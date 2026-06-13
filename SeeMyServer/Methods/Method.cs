using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Renci.SshNet;
using Renci.SshNet.Security;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Power;
using Windows.Networking;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace SeeMyServer.Methods
{
    public class Method
    {
        public static string[] SendSSHCommands(string[] sshCommands, CMSModel cmsModel, string passwd)
        {
            try
            {
                int port;
                if (!int.TryParse(cmsModel.HostPort, out port))
                {
                    Logger.Instance.LogError($"{cmsModel.HostIP}:{cmsModel.HostPort} Invalid SSH port number used: {cmsModel.HostPort}");
                    return null;
                }

                bool usePrivateKey = string.Equals(cmsModel.SSHKeyIsOpen, "True", StringComparison.OrdinalIgnoreCase);
                // 仅使用数据库中的SSHKeyId（不再回退到旧版文件路径）
                string sshKey = cmsModel.SSHKeyId;
                // 注意使用传入的passwd，Model中的Passwd是加密后的结果
                using (SshClient sshClient = InitializeSshClient(cmsModel.HostIP, port, cmsModel.SSHUser, passwd, sshKey, usePrivateKey))
                {
                    if (sshClient == null)
                    {
                        Logger.Instance.LogError($"{cmsModel.HostIP}:{cmsModel.HostPort} SSH client initialization failed.");
                        return null;
                    }

                    return ExecuteSshCommands(sshClient, sshCommands);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"{cmsModel.HostIP}:{cmsModel.HostPort} SSH operation failed: " + ex.Message);
                return null;
            }
        }


        private static SshClient InitializeSshClient(string sshHost, int sshPort, string sshUser, string sshPasswd, string sshKey, bool usePrivateKey)
        {
            try
            {
                if (usePrivateKey)
                {
                    PrivateKeyFile privateKeyFile = LoadPrivateKeyFile(sshKey);
                    ConnectionInfo connectionInfo = new ConnectionInfo(sshHost, sshPort, sshUser, new PrivateKeyAuthenticationMethod(sshUser, new PrivateKeyFile[] { privateKeyFile }));
                    connectionInfo.Encoding = Encoding.UTF8;
                    // 设置连接超时时间
                    connectionInfo.Timeout = TimeSpan.FromSeconds(5);
                    // 设置连接重试次数
                    connectionInfo.RetryAttempts = 3;
                    return new SshClient(connectionInfo);
                }
                else
                {
                    return new SshClient(sshHost, sshPort, sshUser, sshPasswd);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"{sshHost}:{sshPort} SSH 连接失败：" + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 根据SSHKeyId从数据库加载私钥
        /// </summary>
        private static PrivateKeyFile LoadPrivateKeyFile(string sshKey)
        {
            if (string.IsNullOrWhiteSpace(sshKey))
            {
                throw new InvalidOperationException("未配置 SSH 密钥。");
            }

            // SSHKeyId 为纯数字，从数据库加载
            if (int.TryParse(sshKey, out int keyId))
            {
                string privateKeyContent = SSHKeyMethod.LoadPrivateKeyFromDB(sshKey);
                byte[] keyBytes = Encoding.UTF8.GetBytes(privateKeyContent);
                var keyStream = new MemoryStream(keyBytes, 0, keyBytes.Length, false, true);
                var keyFile = new PrivateKeyFile(keyStream);
                keyFile.Dispose();
                keyStream.Dispose();
                Array.Clear(keyBytes, 0, keyBytes.Length);
                privateKeyContent = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                return keyFile;
            }

            throw new InvalidOperationException("无法识别的 SSH 密钥格式，请在编辑中重新选择密钥。");
        }

        private static string[] ExecuteSshCommands(SshClient sshClient, string[] sshCommands)
        {
            // 获取 IP 和端口信息
            string host = sshClient.ConnectionInfo.Host;
            int port = sshClient.ConnectionInfo.Port;

            List<string> results = new List<string>();

            try
            {
                sshClient.Connect();
                if (sshClient.IsConnected)
                {
                    foreach (var sshCommand in sshCommands)
                    {
                        SshCommand SSHCommand = sshClient.RunCommand(sshCommand);
                        if (!string.IsNullOrEmpty(SSHCommand.Error))
                        {
                            results.Add($"[CMSError]: {host}:{port} executing command \"{sshCommand}\": {SSHCommand.Error}");
                            Logger.Instance.LogError($"[CMSError]: {host}:{port} executing command \"{sshCommand}\": {SSHCommand.Error}");
                        }
                        else
                        {
                            results.Add($"{SSHCommand.Result}");
                        }
                    }
                }
                else
                {
                    Logger.Instance.LogError($"{host}:{port} SSH connection failed.");
                    results.Add($"{host}:{port} SSH connection failed.");
                }
            }
            finally
            {
                sshClient.Disconnect();
            }

            return results.ToArray();
        }

        /// <summary>
        /// 解密SSH密码
        /// </summary>
        private static string DecryptSSHPassword(CMSModel cmsModel)
        {
            if (string.IsNullOrEmpty(cmsModel.SSHPasswd))
                return "";

            string key = LoadKeyFromLocalSettings();
            string iv = LoadIVFromLocalSettings();

            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            {
                Logger.Instance.LogError($"{cmsModel.HostIP}:{cmsModel.HostPort} The key and/or initialization vector do not exist.");
                return "";
            }

            using (SymmetricAlgorithm symmetricAlgorithm = Aes.Create())
            {
                symmetricAlgorithm.Key = Convert.FromBase64String(key);
                symmetricAlgorithm.IV = Convert.FromBase64String(iv);
                return DecryptString(cmsModel.SSHPasswd, symmetricAlgorithm);
            }
        }

        private static async Task<string[]> SendSSHCommandAsync(string[] commands, CMSModel cmsModel)
        {
            string passwd = "";
            // 没有打开Key认证
            if (cmsModel.SSHKeyIsOpen != "True")
            {
                if (!string.IsNullOrEmpty(cmsModel.SSHPasswd))
                {
                    Logger.Instance.LogInfo($"{cmsModel.HostIP}:{cmsModel.HostPort} SSH using Password.");
                    passwd = DecryptSSHPassword(cmsModel);
                }
            }
            // 打开Key认证
            else if (cmsModel.SSHKeyIsOpen == "True")
            {
                Logger.Instance.LogInfo($"{cmsModel.HostIP}:{cmsModel.HostPort} SSH using Key authentication.");
            }
            // 其余情况
            else
            {
                Logger.Instance.LogError($"{cmsModel.HostIP}:{cmsModel.HostPort} Unknown SSH login solution.");
            }
            // 异步执行命令
            return await Task.Run(() =>
            {
                return SendSSHCommands(commands, cmsModel, passwd);
            });
        }

        // 导出配置
        public static async Task<string> ExportConfig(CMSModel cmsModel)
        {
            // 创建一个FileSavePicker
            FileSavePicker savePicker = new FileSavePicker();
            // 获取当前窗口句柄 (HWND) 
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            // 使用窗口句柄 (HWND) 初始化FileSavePicker
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            // 为FilePicker设置选项
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            // 用户可以将文件另存为的文件类型下拉列表
            savePicker.FileTypeChoices.Add("Plain Text", new List<string>() { ".cmsconfig" });
            // 如果用户没有选择文件类型，则默认为
            savePicker.DefaultFileExtension = ".cmsconfig";

            // 默认文件名
            savePicker.SuggestedFileName = cmsModel.Name + "_BackUp_" + DateTime.Now.ToString();

            // 打开Picker供用户选择文件
            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    // 阻止更新文件的远程版本，直到我们完成更改并调用 CompleteUpdatesAsync。
                    CachedFileManager.DeferUpdates(file);
                }
                catch
                {
                    // 当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。
                    Logger.Instance.LogWarning($"{cmsModel.Name} 保存行为完成，但当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。");
                    return "保存行为完成，但当您保存至OneDrive等同步盘目录时，在Windows11上可能引起DeferUpdates错误，备份文件不一定写入正确。";
                }

                // 将数据序列化为 JSON 格式
                // 注意：不再导出 SSHKey（旧版文件路径），也不导出 SSHPasswd 和 SSHKeyId
                // 导入旧配置文件时，SSHKey 字段仍会被兼容读取
                var jsonData = JsonConvert.SerializeObject(new
                {
                    cmsModel.Name,
                    cmsModel.HostIP,
                    cmsModel.HostPort,
                    cmsModel.SSHUser,
                    cmsModel.OSType,
                    cmsModel.SSHKeyIsOpen,
                    cmsModel.CPUUsage,
                    cmsModel.MEMUsage,
                    cmsModel.NETSent,
                    cmsModel.NETReceived,
                });

                // 写入文件
                await FileIO.WriteTextAsync(file, jsonData);

                // 让Windows知道我们已完成文件更改，以便其他应用程序可以更新文件的远程版本。
                // 完成更新可能需要Windows请求用户输入。
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)
                {
                    // 保存成功
                    Logger.Instance.LogInfo($"{cmsModel.Name} 保存成功");
                    return $"{cmsModel.Name} 保存成功";
                }
                else if (status == FileUpdateStatus.CompleteAndRenamed)
                {
                    // 重命名并保存成功
                    Logger.Instance.LogInfo($"{cmsModel.Name} 重命名并保存成功");
                    return $"{cmsModel.Name} 重命名并保存成功";
                }
                else
                {
                    // 文件无法保存！
                    Logger.Instance.LogError($"{cmsModel.Name} 无法保存！");
                    return $"{cmsModel.Name} 无法保存！";
                }
            }
            Logger.Instance.LogError($"{cmsModel.Name} 错误！");
            return $"{cmsModel.Name} 错误！";
        }
        // 导入配置
        public static async Task<CMSModel> ImportConfig()
        {
            // 创建一个FileOpenPicker
            var openPicker = new FileOpenPicker();
            // 获取当前窗口句柄 (HWND) 
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            // 使用窗口句柄 (HWND) 初始化FileOpenPicker
            WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hWnd);

            // 为FilePicker设置选项
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            // 建议打开位置 桌面
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            // 文件类型过滤器
            openPicker.FileTypeFilter.Add(".cmsconfig");

            // 打开选择器供用户选择文件
            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                // 读取 JSON 文件内容
                string jsonData = await FileIO.ReadTextAsync(file);
                // 反序列化JSON数据为WoLModel对象
                CMSModel importedData = JsonConvert.DeserializeObject<CMSModel>(jsonData);
                if (importedData != null)
                {
                    // 成功导入配置数据。 
                    return importedData;
                }
                else
                {
                    // JSON数据无法反序列化为配置数据。 
                    return null;
                }
            }
            else
            {
                // 未选择JSON文件。
                return null;
            }
        }

        // CPU结果解析
        // cpu  697687 0 1332141 93898629 1722210 0 840664 0 0 0
        // cpu0 171727 0 309858 23571901 565476 0 3820 0 0 0
        // cpu1 163341 0 297540 23583515 578130 0 277 0 0 0
        // cpu2 155832 0 299665 23203048 129886 0 834464 0 0 0
        // cpu3 206787 0 425078 23540165 448718 0 2103 0 0 0
        //
        // CPU 用户态 用户态低优先级 系统态 空闲 I/O等待 无意义 硬件中断 软件中断 steal_time guest_nice进程
        public static List<List<string>> CPUUsageResult(string CPUUsagesRev, string CPUUsagesRev2)
        {
            List<List<string>> cpuUsageList = new List<List<string>>();
            List<List<string>> cpuUsageList0s = new List<List<string>>();
            List<List<long>> cpuUsageListAbs = new List<List<long>>();

            // 解析结果
            if (CPUUsagesRev.StartsWith("cpu"))
            {
                // 以换行符为准，按行分割结果
                string[] lines = CPUUsagesRev.Split('\n');
                // 遍历每行
                foreach (string line in lines)
                {
                    // 检查是否以 cpu 开头
                    if (line.StartsWith("cpu"))
                    {
                        // 以空格分割，并去除空白项
                        string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        // 保存当前 CPU 的使用情况
                        List<string> cpuUsage = new List<string>();

                        // 计算CPU总事件（单行之和）
                        long totalCpuTime = 0;
                        for (int i = 1; i < fields.Length; i++)
                        {
                            if (long.TryParse(fields[i], out long cpuTime))
                            {
                                totalCpuTime += cpuTime;
                            }
                        }

                        // 安全访问字段，缺失的字段默认为"0"
                        string GetField(int index) => index < fields.Length ? fields[index] : "0";

                        cpuUsage.Add(GetField(1));    //0 用户态
                        cpuUsage.Add(GetField(2));    //1 用户态低优先级
                        cpuUsage.Add(GetField(3));    //2 系统态
                        cpuUsage.Add(GetField(4));    //3 空闲
                        cpuUsage.Add(GetField(5));    //4 I/O等待
                        cpuUsage.Add(GetField(6));    //5 无意义
                        cpuUsage.Add(GetField(7));    //6 硬件中断
                        cpuUsage.Add(GetField(8));    //7 软件中断
                        cpuUsage.Add(GetField(9));    //8 steal_time
                        cpuUsage.Add(GetField(10));   //9 guest_nice进程
                        cpuUsage.Add($"{totalCpuTime}"); //10 总时间

                        cpuUsageList0s.Add(cpuUsage);
                    }
                }
            }

            if (CPUUsagesRev.StartsWith("cpu"))
            {
                // 以换行符为准，按行分割结果
                string[] lines2 = CPUUsagesRev2.Split('\n');
                // 遍历每行
                int index = 0;
                foreach (string line in lines2)
                {
                    // 检查是否以 cpu 开头
                    if (line.StartsWith("cpu"))
                    {
                        // 以空格分割，并去除空白项
                        string[] fields = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        // 保存当前 CPU 的使用情况
                        List<long> cpuUsage = new List<long>();

                        // 计算CPU总事件（单行之和）
                        long totalCpuTime = 0;
                        for (int i = 1; i < fields.Length; i++)
                        {
                            if (long.TryParse(fields[i], out long cpuTime))
                            {
                                totalCpuTime += cpuTime;
                            }
                        }

                        // 安全获取字段值，缺失的字段默认为"0"
                        long SafeParse(string s) => long.TryParse(s, out long v) ? v : 0L;
                        long SafeParseField(int idx) => idx < fields.Length ? SafeParse(fields[idx]) : 0L;

                        // 计算差值（使用 long 防止溢出）
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][0]) - SafeParseField(1)));    //0 用户态
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][1]) - SafeParseField(2)));    //1 用户态低优先级
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][2]) - SafeParseField(3)));    //2 系统态
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][3]) - SafeParseField(4)));    //3 空闲
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][4]) - SafeParseField(5)));    //4 I/O等待
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][5]) - SafeParseField(6)));    //5 无意义
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][6]) - SafeParseField(7)));    //6 硬件中断
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][7]) - SafeParseField(8)));    //7 软件中断
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][8]) - SafeParseField(9)));    //8 steal_time
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][9]) - SafeParseField(10)));   //9 guest_nice进程
                        cpuUsage.Add(Math.Abs(SafeParse(cpuUsageList0s[index][10]) - totalCpuTime));       //10 总时间

                        cpuUsageListAbs.Add(cpuUsage);

                        index++;
                    }
                }
            }

            // 计算占用率
            foreach (List<long> cpuUsageAbs in cpuUsageListAbs)
            {
                // 保存当前 CPU 的使用情况
                List<string> cpuUsage = new List<string>();
                // 占用率计算
                cpuUsage.Add($"{100 - ((double)cpuUsageAbs[3] / (double)cpuUsageAbs[10] * 100):F0}");    //0 CPU占用率
                cpuUsage.Add($"{((double)cpuUsageAbs[0]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUUser占用率
                cpuUsage.Add($"{((double)cpuUsageAbs[2]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUSys占用率
                cpuUsage.Add($"{((double)cpuUsageAbs[3]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUIdle占用率
                cpuUsage.Add($"{((double)cpuUsageAbs[4]) / (double)cpuUsageAbs[10] * 100:F2}");    //0 CPUIO占用率

                cpuUsageList.Add(cpuUsage);
            }
            return cpuUsageList;
        }

        // 内存结果解析
        // MemTotal:        3902716 kB
        // MemFree:          151924 kB
        // MemAvailable:    2799072 kB
        // SwapCached:        66680 kB
        // SwapTotal:       4439980 kB
        // SwapFree:        3741944 kB
        public static List<string> MemUsageResult(string MEMUsagesRev)
        {
            List<string> parsedResults = new List<string>();
            // 定义正则表达式模式
            Regex pattern = new Regex(@"(\w+):\s+(\d+)\s+(\w+)");

            // 使用正则表达式进行匹配
            MatchCollection matches = pattern.Matches(MEMUsagesRev);

            // 遍历匹配结果
            foreach (Match match in matches)
            {
                // 检查匹配是否成功
                if (match.Success)
                {
                    string matchResult = $"{match.Groups[2].Value}";
                    parsedResults.Add(matchResult);
                }
                else
                {
                    Logger.Instance.LogError("MEMUsagesRev pattern match failed.");
                }
            }

            return parsedResults;
        }

        // 网卡信息结果解析
        public static List<NetworkInterfaceInfo> NetworkInterfaceInfosResult(string NETUsagesRev, string NETUsagesRev2, Stopwatch stopwatch)
        {
            List<NetworkInterfaceInfo> networkInterfaceInfos = new List<NetworkInterfaceInfo>();
            networkInterfaceInfos = NetworkInterfaceInfoParse(NETUsagesRev, NETUsagesRev2, stopwatch.ElapsedMilliseconds);
            return networkInterfaceInfos;
        }

        // 挂载信息结果解析
        public static List<List<MountInfo>> MountInfosResult(string DFUsagesRev, string DiskStatsRev, string DiskStatsRev2, Stopwatch stopwatch)
        {
            List<List<MountInfo>> mountInfos = new List<List<MountInfo>>();
            mountInfos = MountInfoParse(DFUsagesRev, DiskStatsRev, DiskStatsRev2, stopwatch.ElapsedMilliseconds);
            return mountInfos;
        }

        // 负载结果解析
        // 解析 top -bn1 输出中的进程列表（自动适配不同列排序）
        public static List<TopProcessInfo> ParseTopProcesses(string topOutput)
        {
            var processes = new List<TopProcessInfo>();
            if (string.IsNullOrEmpty(topOutput)) return processes;

            string[] lines = topOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            // 列名 → 索引映射
            int pidIdx = -1, userIdx = -1, cpuIdx = -1, memIdx = -1;
            int timeIdx = -1, commandIdx = -1, statusIdx = -1;
            int virtIdx = -1, resIdx = -1, shrIdx = -1;
            bool headerFound = false;

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                // 找到表头行
                if (!headerFound)
                {
                    if (!trimmed.Contains("PID") || !trimmed.Contains("COMMAND"))
                        continue;
                    headerFound = true;
                    string[] cols = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < cols.Length; i++)
                    {
                        string name = cols[i].Trim().ToUpperInvariant();
                        switch (name)
                        {
                            case "PID": pidIdx = i; break;
                            case "USER": userIdx = i; break;
                            case "%CPU": cpuIdx = i; break;
                            case "TIME+": timeIdx = i; break;
                            case "TIME": timeIdx = i; break;
                            case "COMMAND": commandIdx = i; break;
                            case "S": statusIdx = i; break;
                            case "STAT": statusIdx = i; break;
                            case "VIRT": virtIdx = i; break;
                            case "VSZ": virtIdx = i; break;
                            case "RES": resIdx = i; break;
                            case "RSS": resIdx = i; break;
                            case "SHR": shrIdx = i; break;
                            case "%VSZ": if (memIdx < 0) memIdx = i; break;
                            case "%MEM": memIdx = i; break;
                        }
                    }
                    continue;
                }

                // 解析数据行
                string[] parts = trimmed.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (pidIdx < 0 || parts.Length <= pidIdx || !int.TryParse(parts[pidIdx], out _))
                    continue;

                string time = timeIdx >= 0 && timeIdx < parts.Length ? parts[timeIdx] : "-";

                string status = statusIdx >= 0 && statusIdx < parts.Length ? parts[statusIdx] : "-";
                string statusText = status switch
                {
                    "R" => "运行",
                    "S" => "睡眠",
                    "D" => "不可中断",
                    "Z" => "僵尸",
                    "T" => "停止",
                    "I" => "空闲",
                    _ => status
                };

                string command = commandIdx >= 0 && commandIdx < parts.Length
                    ? string.Join(" ", parts, commandIdx, parts.Length - commandIdx).TrimEnd('+')
                    : "";

                processes.Add(new TopProcessInfo
                {
                    PID = parts[pidIdx],
                    User = userIdx >= 0 && userIdx < parts.Length ? parts[userIdx] : "",
                    CPUPercent = cpuIdx >= 0 && cpuIdx < parts.Length ? parts[cpuIdx] : "0",
                    MEMPercent = memIdx >= 0 && memIdx < parts.Length ? parts[memIdx] : "0",
                    Time = time,
                    Command = command,
                    Status = statusText,
                    VirtMem = virtIdx >= 0 && virtIdx < parts.Length ? parts[virtIdx] : "",
                    ResMem = resIdx >= 0 && resIdx < parts.Length ? parts[resIdx] : "",
                    SharedMem = shrIdx >= 0 && shrIdx < parts.Length ? parts[shrIdx] : ""
                });
            }
            return processes;
        }

        // 不同主机的top格式可能不同，大多数Linux发行版可能相同，OpenWRT一般不同，这里注意特殊处理。
        public static List<string> LoadAverageResult(string CoreNumRev, string TopRev)
        {
            List<string> loadResults = new List<string>();
            double average1 = .0;
            double average5 = .0;
            double average15 = .0;
            double average1Percentage = .0;
            double average5Percentage = .0;
            double average15Percentage = .0;
            string CPUCoreRes = CoreNumRev;

            // OpenWRT单独适配
            if (TopRev.StartsWith("Mem"))
            {
                Logger.Instance.LogInfo("Load average (OpenWRT).");
                // 使用正则取出负载信息
                Regex loadRegex = new Regex(@"Load average: (\d+\.\d+) (\d+\.\d+) (\d+\.\d+) (\d+)/(\d+) (\d+)");

                Match loadMatch = loadRegex.Match(TopRev);
                try
                {
                    // 获取1分钟内负载
                    average1 = double.Parse(loadMatch.Groups[1].Value);
                    // 获取5分钟内负载
                    average5 = double.Parse(loadMatch.Groups[2].Value);
                    // 获取15分钟内负载
                    average15 = double.Parse(loadMatch.Groups[3].Value);

                    // 计算负载
                    // 此处的计算基于Load average定义，每个核心有一个任务在执行是最佳满载状态(100%)
                    // 1分钟内
                    average1Percentage = average1 * 100 / double.Parse(CPUCoreRes);
                    // 5分钟内
                    average5Percentage = average5 * 100 / double.Parse(CPUCoreRes);
                    // 15分钟内
                    average15Percentage = average15 * 100 / double.Parse(CPUCoreRes);
                }
                catch (Exception ex)
                {
                    //double.Parse(CPUCoreRes)失败
                    Logger.Instance.LogError(ex.Message);
                }
            }
            // 负载信息
            else
            {
                // 使用正则取出负载信息
                Regex loadRegex = new Regex(@"load average: (\d+\.\d+), (\d+\.\d+), (\d+\.\d+)");

                Match loadMatch = loadRegex.Match(TopRev);

                try
                {
                    // 获取1分钟内负载
                    average1 = double.Parse(loadMatch.Groups[1].Value);
                    // 获取5分钟内负载
                    average5 = double.Parse(loadMatch.Groups[2].Value);
                    // 获取15分钟内负载
                    average15 = double.Parse(loadMatch.Groups[3].Value);

                    // 此处的计算基于Load average定义，每个核心有一个任务在执行是最佳满载状态(100%)
                    // 1分钟内
                    average1Percentage = average1 * 100 / double.Parse(CPUCoreRes);
                    // 5分钟内
                    average5Percentage = average5 * 100 / double.Parse(CPUCoreRes);
                    // 15分钟内
                    average15Percentage = average15 * 100 / double.Parse(CPUCoreRes);
                }
                catch (Exception ex)
                {
                    //double.Parse(CPUCoreRes)失败
                    Logger.Instance.LogError(ex.Message);
                }
            }
            // 将结果添加到 List<string> 中
            loadResults.Add($"{average1:F2}");
            loadResults.Add($"{average5:F2}");
            loadResults.Add($"{average15:F2}");
            loadResults.Add($"{average1Percentage:F2}");
            loadResults.Add($"{average5Percentage:F2}");
            loadResults.Add($"{average15Percentage:F2}");

            return loadResults;
        }

        // 其他信息
        public static async Task<List<string>> AboutInfoResult(string HostnameRev, string UptimeRev, string CoreNumRev, string OSReleaseRev, string TopRev, string LinuxKernelVersion, CMSModel cmsModel)
        {
            List<string> aboutInfo = new List<string>();

            // 启动时长
            aboutInfo.Add(UptimeRev.Split(',')[0]);
            // 主机名
            // 单独处理OpenWRT的情况
            if (HostnameRev != "" && HostnameRev != null && !HostnameRev.StartsWith("[CMSError]:"))
            {
                aboutInfo.Add(HostnameRev.Split('\n')[0]);
            }
            else
            {
                Logger.Instance.LogError("HostName acquisition failed, try to use 'uci get system.@system[0].hostname'.");
                string[] CMD = new string[]
                {
                "uci get system.@system[0].hostname"
                };
                HostnameRev = (await SendSSHCommandAsync(CMD, cmsModel).ConfigureAwait(false)).FirstOrDefault();

                // 如果结果仍然错误
                if (HostnameRev != "" && HostnameRev != null && !HostnameRev.StartsWith("[CMSError]:"))
                {
                    aboutInfo.Add(HostnameRev.Split('\n')[0]);
                    Logger.Instance.LogError("HostName acquisition failed.");
                }
                else
                {
                    aboutInfo.Add("");
                }
            }
            // 核心数量
            aboutInfo.Add(CoreNumRev);
            // 系统版本
            if (OSReleaseRev != "" && OSReleaseRev != null && !OSReleaseRev.StartsWith("[CMSError]:"))
            {
                // 检索到系统版本
                aboutInfo.Add(OSReleaseRev.Split('\"')[1]);
            }
            else
            {
                // 检索不到系统版本
                aboutInfo.Add("");
                Logger.Instance.LogError("OSRelease acquisition failed.");
            }
            // top
            aboutInfo.Add(TopRev);
            // 内核版本
            LinuxKernelVersion = LinuxKernelVersion.TrimEnd('\n', '\r');
            aboutInfo.Add(LinuxKernelVersion);

            return aboutInfo;
        }

        // 获取Linux系统信息
        public static async Task<Tuple<
        List<List<string>>,
        List<string>,
        List<NetworkInterfaceInfo>,
        List<List<MountInfo>>,
        List<string>,
        List<string>
        >> GetLinuxCPUUsageAsync(CMSModel cmsModel)
        {
            // 用于保存结果的List
            List<List<string>> cpuUsageList = new List<List<string>>();
            List<string> parsedResults = new List<string>();
            List<NetworkInterfaceInfo> networkInterfaceInfos = new List<NetworkInterfaceInfo>();
            List<List<MountInfo>> mountInfos = new List<List<MountInfo>>();
            List<string> loadResults = new List<string>();
            List<string> aboutInfo = new List<string>();

            // 创建 Stopwatch 实例
            Stopwatch stopwatch = new Stopwatch();

            // OpenWRT不能用hostname，可以用"uci get system.@system[0].hostname"
            // 用户应自己设置命令别名以兼容
            // P - 防止换行
            // 2>&1 - 将标准错误输出重定向到标准输出，这样可以在管道中处理错误消息。
            // 命令列表
            string[] CPUUsageCMD = new string[]
            {
            "cat /proc/stat | grep cpu",
            "cat /proc/meminfo | grep -E 'Mem|Swap'",
            "cat /proc/net/dev",
            "df -hP",
            "uptime | awk '{print $3 \" \" $4}'",
            "hostname",
            "top -bn1 -w 512",
            "cat /proc/cpuinfo | grep processor | wc -l",
            "cat /etc/*-release | grep PRETTY_NAME",
            "cat /proc/diskstats",
            "uname -r"
            };
            string[] result = await SendSSHCommandAsync(CPUUsageCMD, cmsModel).ConfigureAwait(false);
            // 开始计时
            stopwatch.Start();
            try
            {
                if (result != null)
                {
                    string CPUUsagesRev = result[0];
                    string MEMUsagesRev = result[1];
                    string NETUsagesRev = result[2];
                    string DFUsagesRev = result[3];
                    string UptimeRev = result[4];
                    string HostnameRev = result[5];
                    string TopRev = result[6];
                    string CoreNumRev = result[7];
                    string OSReleaseRev = result[8];
                    string DiskStatsRev = result[9];
                    string LinuxKernelVersion = result[10];

                    await Task.Delay(1000).ConfigureAwait(false);

                    string[] result2 = await SendSSHCommandAsync(CPUUsageCMD, cmsModel).ConfigureAwait(false);
                    // 停止计时
                    stopwatch.Stop();

                    if (result2 != null)
                    {

                        string CPUUsagesRev2 = result2[0];
                        string MEMUsagesRev2 = result2[1];
                        string NETUsagesRev2 = result2[2];
                        string DFUsagesRev2 = result2[3];
                        string UptimeRev2 = result2[4];
                        string HostnameRev2 = result2[5];
                        string TopRev2 = result2[6];
                        string CoreNumRev2 = result2[7];
                        string OSReleaseRev2 = result2[8];
                        string DiskStatsRev2 = result2[9];
                        string LinuxKernelVersion2 = result2[10];

                        // CPU占用
                        cpuUsageList = CPUUsageResult(CPUUsagesRev, CPUUsagesRev2);
                        // 内存和swap占用
                        parsedResults = MemUsageResult(MEMUsagesRev);
                        // 网卡信息
                        networkInterfaceInfos = NetworkInterfaceInfosResult(NETUsagesRev, NETUsagesRev2, stopwatch);
                        // 挂载信息
                        mountInfos = MountInfosResult(DFUsagesRev, DiskStatsRev, DiskStatsRev2, stopwatch);
                        // 启动时长、主机名、CPU核心数量、系统版本、top
                        aboutInfo = await AboutInfoResult(HostnameRev, UptimeRev, CoreNumRev, OSReleaseRev, TopRev, LinuxKernelVersion, cmsModel);
                        // 负载信息
                        loadResults = LoadAverageResult(CoreNumRev, TopRev);
                    }
                    else
                    {
                        Logger.Instance.LogError("The number of elements in the SSH result array is incorrect.");
                        Logger.Instance.LogError(string.Join("\n\n", result));
                    }

                    return Tuple.Create(cpuUsageList, parsedResults, networkInterfaceInfos, mountInfos, aboutInfo, loadResults);
                }
                else
                {
                    // 停止计时
                    stopwatch.Stop();

                    Logger.Instance.LogError("SSH results array is null.");
                    return null;
                }
            }
            finally
            {
                // 确保释放信号量
                cmsModel.UpdateSemaphore.Release();
            }
        }

        /// <summary>
        /// 更新 CMSModel 的监控数据（CPU、内存、网络、磁盘等）
        /// 此方法提取自 HomePage 和 DetailPage 的重复逻辑
        /// </summary>
        public static void UpdateCMSModelFromUsageResult(
            CMSModel cmsModel,
            Tuple<List<List<string>>, List<string>, List<NetworkInterfaceInfo>, List<List<MountInfo>>, List<string>, List<string>> Usages)
        {
            if (Usages == null) return;

            var cpuUsages = Usages.Item1;
            var memUsages = Usages.Item2;
            var NetworkInterfaceInfos = Usages.Item3;
            var MountInfos = Usages.Item4[0];
            var DiskStatus = Usages.Item4[1];
            var UpTime = Usages.Item5[0];
            var HostName = Usages.Item5[1];
            var CPUCoreNum = Usages.Item5[2];
            var PRETTY_NAME = Usages.Item5[3];
            var TOPRec = Usages.Item5[4];
            var LinuxKernelVersion = Usages.Item5[5];
            var loadAverage = Usages.Item6;

            // 只有HostName和OSRelease为空才更新
            if (string.IsNullOrEmpty(cmsModel.HostName))
                cmsModel.HostName = HostName;
            cmsModel.UpTime = UpTime;
            if (string.IsNullOrEmpty(cmsModel.OSRelease))
                cmsModel.OSRelease = PRETTY_NAME;
            if (string.IsNullOrEmpty(cmsModel.CPUCoreNum))
                cmsModel.CPUCoreNum = CPUCoreNum;
            cmsModel.TopRes = TOPRec;
            cmsModel.LinuxKernelVersionRes = LinuxKernelVersion;

            // 解析 TOP 进程列表（增量更新，按序复用避免 Clear+Add 重建视觉树）
            try
            {
                var parsed = ParseTopProcesses(TOPRec);
                var existing = cmsModel.TopProcesses;
                int parsedCount = parsed.Count;

                for (int i = 0; i < parsedCount; i++)
                {
                    if (i < existing.Count)
                    {
                        var e = existing[i];
                        e.CPUPercent = parsed[i].CPUPercent;
                        e.MEMPercent = parsed[i].MEMPercent;
                        e.Time = parsed[i].Time;
                        e.Status = parsed[i].Status;
                        e.Command = parsed[i].Command;
                        e.User = parsed[i].User;
                        e.VirtMem = parsed[i].VirtMem;
                        e.ResMem = parsed[i].ResMem;
                        e.SharedMem = parsed[i].SharedMem;
                    }
                    else
                    {
                        existing.Add(parsed[i]);
                    }
                }
                while (existing.Count > parsedCount)
                    existing.RemoveAt(existing.Count - 1);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"TOP parse failed: {ex.Message}");
            }

            // 处理CPU数据
            try
            {
                cmsModel.CPUUsage = $"{cpuUsages[0][0]}%";
                cmsModel.CPUCoreNum = CPUCoreNum.Split('\n')[0];
                cmsModel.CPUUserUsage = $"{cpuUsages[0][1]}%";
                cmsModel.CPUSysUsage = $"{cpuUsages[0][2]}%";
                cmsModel.CPUIdleUsage = $"{cpuUsages[0][3]}%";
                cmsModel.CPUIOUsage = $"{cpuUsages[0][4]}%";
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"CPU data update failed: {ex.Message}");
            }

            // 负载信息 - 获取结果失败不更新
            if (loadAverage[3] != "0" || loadAverage[4] != "0" || loadAverage[5] != "0")
            {
                cmsModel.Average1 = loadAverage[0];
                cmsModel.Average5 = loadAverage[1];
                cmsModel.Average15 = loadAverage[2];
                cmsModel.Average1Percentage = loadAverage[3];
                cmsModel.Average5Percentage = loadAverage[4];
                cmsModel.Average15Percentage = loadAverage[5];
            }

            // 内存数据
            try
            {
                double memTotal = double.Parse(memUsages[0]);
                double memFree = double.Parse(memUsages[1]);
                double memAvailable = double.Parse(memUsages[2]);

                double memUsagesValue = (memTotal - memAvailable) * 100 / memTotal;
                cmsModel.MEMUsage = $"{memUsagesValue:F0}%";
                double memFreeValue = memFree * 100 / memTotal;
                cmsModel.MEMFree = $"{memFreeValue:F2}%";
                double memAvailableValue = memAvailable * 100 / memTotal;
                cmsModel.MEMAvailable = $"{memAvailableValue:F2}%";
                double memUsagePageCacheValue = memUsagesValue + (memAvailableValue - memFreeValue);
                cmsModel.MEMUsagePageCache = $"{memUsagePageCacheValue:F2}%";
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"MEM data update failed: {ex.Message}");
            }

            // Swap数据
            try
            {
                double swapCached = double.Parse(memUsages[3]);
                double swapTotal = double.Parse(memUsages[4]);
                double swapFree = double.Parse(memUsages[5]);

                if (swapTotal != 0)
                {
                    double swapUsagesValue = (swapTotal - swapFree) * 100 / swapTotal;
                    cmsModel.SwapUsage = $"{swapUsagesValue:F0}%";
                    double swapCachedValue = swapCached * 100 / swapTotal;
                    cmsModel.SwapCached = $"{swapCachedValue:F2}%";
                    cmsModel.SwapCachedDisplay = $"{swapUsagesValue + swapCachedValue:F2}%";
                }
                else
                {
                    cmsModel.SwapUsage = "0%";
                    cmsModel.SwapCached = "0%";
                    cmsModel.SwapCachedDisplay = "0%";
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Swap data update failed: {ex.Message}");
            }

            // 总内存/总Swap
            try
            {
                cmsModel.TotalMEM = $"{NetUnitConversion(decimal.Parse(memUsages[0]) * 1024)}";
                cmsModel.TotalSwap = $"{NetUnitConversion(decimal.Parse(memUsages[4]) * 1024)}";
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"Total MEM/Swap update failed: {ex.Message}");
            }

            // CPU核心令牌
            try
            {
                cmsModel.CPUCoreTokens = cpuUsages.Skip(1).Select(cpuUsage => cpuUsage[0]).ToArray();
            }
            catch (Exception ex)
            {
                Logger.Instance.LogError($"CPUCoreTokens update failed: {ex.Message}");
            }

            // 挂载和网络信息
            cmsModel.MountInfos = MountInfos;
            cmsModel.NetworkInterfaceInfos = NetworkInterfaceInfos;

            // 网络汇总
            cmsModel.NETSent = $"{NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.TransmitSpeedByte))}/s ↑";
            cmsModel.NETReceived = $"{NetUnitConversion(cmsModel.NetworkInterfaceInfos.Sum(iface => iface.ReceiveSpeedByte))}/s ↓";

            // 磁盘汇总
            cmsModel.DISKRead = $"{NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsReadPerSecondOrigin))}/s R";
            cmsModel.DISKWrite = $"{NetUnitConversion(DiskStatus.Sum(dstatus => dstatus.SectorsWrittenPerSecondOrigin))}/s W";
        }

        public static string NetUnitConversion(decimal netValue)
        {
            decimal result;
            string unit;

            switch (netValue)
            {
                case decimal n when n >= 1000000000000:
                    result = netValue / 1024m / 1024m / 1024m / 1024m;
                    unit = "TB";
                    break;
                case decimal n when n >= 1000000000:
                    result = netValue / 1024m / 1024m / 1024m;
                    unit = "GB";
                    break;
                case decimal n when n >= 1000000:
                    result = netValue / 1024m / 1024m;
                    unit = "MB";
                    break;
                case decimal n when n >= 1000:
                    result = netValue / 1024m;
                    unit = "KB";
                    break;
                default:
                    result = netValue;
                    unit = "B";
                    break;
            }

            return result.ToString("F2") + " " + unit;
        }
        public static decimal ReverseNetUnitConversion(string convertedValue)
        {
            string[] parts = convertedValue.Split(' ');
            decimal value = decimal.Parse(parts[0]);
            string unit = parts[1];

            switch (unit)
            {
                case "TB":
                    return value * 1024m * 1024m * 1024m * 1024m;
                case "GB":
                    return value * 1024m * 1024m * 1024m;
                case "MB":
                    return value * 1024m * 1024m;
                case "KB":
                    return value * 1024m;
                default:
                    return value;
            }
        }
        // 处理 df
        public static List<List<MountInfo>> MountInfoParse(string input, string diskStatus1, string diskStatus2, decimal elapsedTime)
        {
            var mountInfos = new List<MountInfo>();
            var result = new List<List<MountInfo>>();

            // 按行分割输入
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            List<MountInfo> status = ParseDiskSpeed(diskStatus1, diskStatus2, elapsedTime);

            // 跳过标题行
            foreach (var line in lines.Skip(1))
            {
                // 检查是否以 "/" 开头
                // 检查是否包含 ":" （兼容WSL）
                if (line.StartsWith("/") || line.Substring(1).StartsWith(":"))
                {
                    var columns = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    // 共6列
                    if (columns.Length == 6)
                    {
                        var mountInfo = new MountInfo
                        {
                            FileSystem = columns[0],
                            Size = columns[1],
                            Used = columns[2],
                            Avail = columns[3],
                            UsePercentage = columns[4],
                            MountedOn = columns[5]
                        };

                        // Find corresponding status for the current FileSystem
                        var correspondingStatus = status.FirstOrDefault(s => s.FileSystem == mountInfo.FileSystem);
                        if (correspondingStatus != null)
                        {
                            mountInfo.SectorsRead = correspondingStatus.SectorsRead;
                            mountInfo.SectorsWritten = correspondingStatus.SectorsWritten;
                            mountInfo.SectorsReadBytes = $"{NetUnitConversion(correspondingStatus.SectorsRead * 512m)}";
                            mountInfo.SectorsWrittenBytes = $"{NetUnitConversion(correspondingStatus.SectorsWritten * 512m)}";
                            mountInfo.SectorsReadPerSecondOrigin = correspondingStatus.SectorsReadPerSecondOrigin;
                            mountInfo.SectorsWrittenPerSecondOrigin = correspondingStatus.SectorsWrittenPerSecondOrigin;
                            mountInfo.SectorsReadPerSecond = correspondingStatus.SectorsReadPerSecond;
                            mountInfo.SectorsWrittenPerSecond = correspondingStatus.SectorsWrittenPerSecond;
                        }
                        else
                        {
                            // Log an error if corresponding status not found
                            //Logger.Instance.LogError($"Corresponding status not found for FileSystem: {mountInfo.FileSystem}");
                        }

                        mountInfos.Add(mountInfo);
                    }
                    else
                    {
                        //Logger.Instance.LogError($"Invalid line format: {line}");
                    }
                }
            }
            result.Add(mountInfos);
            result.Add(status);
            return result;
        }

        private static List<MountInfo> ParseSingleDiskStatus(string input)
        {
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var diskStatusInfos = new List<MountInfo>();

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var info = new MountInfo
                {
                    FileSystem = $"/dev/{parts[2]}",
                    SectorsRead = long.Parse(parts[5]),
                    SectorsWritten = long.Parse(parts[9]),
                };

                diskStatusInfos.Add(info);
            }

            //throw new Exception($"{interfaceInfos[0].Interface}");
            return diskStatusInfos;
        }
        private static List<MountInfo> ParseDiskSpeed(string input1, string input2, decimal elapsedTime)
        {
            var diskStatus1 = ParseSingleDiskStatus(input1);
            var diskStatus2 = ParseSingleDiskStatus(input2);
            var diskStatusInfos = new List<MountInfo>();

            foreach (var dstatus1 in diskStatus1)
            {
                var dstatus2 = diskStatus2.FirstOrDefault(x => x.FileSystem == dstatus1.FileSystem);
                if (dstatus2 != null)
                {
                    decimal sectorsReadSpeed = (dstatus2.SectorsRead - dstatus1.SectorsRead) * 1000 / elapsedTime;
                    decimal sectorsWrittenSpeed = (dstatus2.SectorsWritten - dstatus1.SectorsWritten) * 1000 / elapsedTime;
                    decimal readBytesPerSecond = sectorsReadSpeed * 512;
                    decimal writeBytesPerSecond = sectorsWrittenSpeed * 512;

                    diskStatusInfos.Add(new MountInfo
                    {
                        FileSystem = dstatus2.FileSystem,
                        SectorsRead = dstatus2.SectorsRead,
                        SectorsWritten = dstatus2.SectorsWritten,
                        SectorsReadPerSecondOrigin = readBytesPerSecond,
                        SectorsWrittenPerSecondOrigin = writeBytesPerSecond,
                        SectorsReadPerSecond = $"{NetUnitConversion(readBytesPerSecond)}/s R",
                        SectorsWrittenPerSecond = $"{NetUnitConversion(writeBytesPerSecond)}/s W",
                    });
                }
            }

            return diskStatusInfos;
        }



        // 处理 ifconfig
        public static List<NetworkInterfaceInfo> NetworkInterfaceInfoParse(string input, string input2, decimal elapsedTime)
        {
            var interfaces1 = ParseSingleNetDev(input);
            var interfaces2 = ParseSingleNetDev(input2);
            var interfaceInfos = new List<NetworkInterfaceInfo>();

            foreach (var iface1 in interfaces1)
            {
                var iface2 = interfaces2.FirstOrDefault(x => x.Interface == iface1.Interface);
                if (iface2 != null)
                {
                    //throw new Exception($"{elapsedTime}");
                    decimal receiveSpeed = (iface2.ReceiveBytesOrigin - iface1.ReceiveBytesOrigin) * 1000 / elapsedTime;
                    decimal transmitSpeed = (iface2.TransmitBytesOrigin - iface1.TransmitBytesOrigin) * 1000 / elapsedTime;

                    interfaceInfos.Add(new NetworkInterfaceInfo
                    {
                        Interface = iface2.Interface,
                        ReceiveBytes = iface2.ReceiveBytes,
                        ReceivePackets = iface2.ReceivePackets,
                        TransmitBytes = iface2.TransmitBytes,
                        TransmitPackets = iface2.TransmitPackets,
                        ReceiveSpeedByte = receiveSpeed,
                        TransmitSpeedByte = transmitSpeed,
                        ReceiveSpeed = $"{NetUnitConversion(receiveSpeed)}/s ↓",
                        TransmitSpeed = $"{NetUnitConversion(transmitSpeed)}/s ↑"
                    });
                }
            }

            return interfaceInfos;
        }
        private static List<NetworkInterfaceInfo> ParseSingleNetDev(string input)
        {
            var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            var interfaceInfos = new List<NetworkInterfaceInfo>();

            foreach (var line in lines.Skip(2))
            {
                var parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                    continue;

                var interfaceName = parts[0].Trim();
                var tokens = parts[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var values = tokens.Select(long.Parse).ToArray();

                var info = new NetworkInterfaceInfo
                {
                    Interface = interfaceName,
                    ReceiveBytes = $"{NetUnitConversion(values[0])}",
                    ReceiveBytesOrigin = values[0],
                    ReceivePackets = $"{values[1]}",
                    TransmitBytes = $"{NetUnitConversion(values[8])}",
                    TransmitBytesOrigin = values[8],
                    TransmitPackets = $"{values[9]}"
                };

                interfaceInfos.Add(info);
            }

            //throw new Exception($"{interfaceInfos[0].Interface}");
            return interfaceInfos;
        }

        public static string EncryptString(string plainText, SymmetricAlgorithm symmetricAlgorithm)
        {
            // 创建加密器
            ICryptoTransform encryptor = symmetricAlgorithm.CreateEncryptor(symmetricAlgorithm.Key, symmetricAlgorithm.IV);

            // 创建内存流，用于写入加密后的数据
            using (MemoryStream memoryStream = new MemoryStream())
            {
                // 创建加密流
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    // 将字符串转换为字节数组并写入加密流
                    byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                    cryptoStream.FlushFinalBlock();
                }
                // 返回加密后的数据，以Base64编码的字符串形式
                return Convert.ToBase64String(memoryStream.ToArray());
            }
        }

        public static string DecryptString(string cipherText, SymmetricAlgorithm symmetricAlgorithm)
        {
            // 创建解密器
            ICryptoTransform decryptor = symmetricAlgorithm.CreateDecryptor(symmetricAlgorithm.Key, symmetricAlgorithm.IV);

            // 创建内存流，用于写入解密后的数据
            using (MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cipherText)))
            {
                // 创建解密流
                using (CryptoStream cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    // 从解密流中读取解密后的字节数组
                    using (StreamReader streamReader = new StreamReader(cryptoStream))
                    {
                        return streamReader.ReadToEnd();
                    }
                }
            }
        }

        public static string LoadKeyFromLocalSettings()
        {
            // 从 localSettings 中加载密钥
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values["Key"] as string;
        }

        public static string LoadIVFromLocalSettings()
        {
            // 从 localSettings 中加载初始化向量
            var localSettings = ApplicationData.Current.LocalSettings;
            return localSettings.Values["IV"] as string;
        }

        public static void SaveKeyToLocalSettings(string key)
        {
            // 将密钥保存到 localSettings 中
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["Key"] = key;
        }

        public static void SaveIVToLocalSettings(string iv)
        {
            // 将初始化向量保存到 localSettings 中
            var localSettings = ApplicationData.Current.LocalSettings;
            localSettings.Values["IV"] = iv;
        }

        public static string GenerateRandomKey()
        {
            // 生成一个随机的密钥
            byte[] key = new byte[32];
            RandomNumberGenerator.Fill(key);
            return Convert.ToBase64String(key);
        }

        public static string GenerateRandomIV()
        {
            // 生成一个随机的初始化向量
            byte[] iv = new byte[16];
            RandomNumberGenerator.Fill(iv);
            return Convert.ToBase64String(iv);
        }

        /// <summary>
        /// 清理遗留的临时私钥文件（兼容旧版本残留）
        /// </summary>
        public static void CleanupTempSSHKeys()
        {
            try
            {
                string tempDir = System.IO.Path.GetTempPath();
                foreach (string file in Directory.GetFiles(tempDir, "SeeMyServer_sshadd_*"))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (string file in Directory.GetFiles(tempDir, "SeeMyServer_SSHKey_*"))
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }
    }
}
