using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SeeMyServer.Helper
{
    public class Logger : IDisposable
    {
        private string logFilePath;
        private int maxLogSize;
        private ConcurrentQueue<string> logQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent logEvent = new AutoResetEvent(false);
        private CancellationTokenSource cts = new CancellationTokenSource();
        private bool disposed = false;

        public Logger(int maxFileSizeMB)
        {
            string userFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string logFolder = Path.Combine(userFolderPath, ".cmslogs");
            logFilePath = Path.Combine(logFolder, "logfile.txt");

            // 将MB转换为字节
            maxLogSize = maxFileSizeMB * 1024 * 1024;

            // 确保目录存在
            Directory.CreateDirectory(logFolder);

            // 启动日志写入线程
            Thread logThread = new Thread(WriteLogThread);
            logThread.IsBackground = true;
            logThread.Start();
        }

        public void LogInfo(string message)
        {
            Log("[INFO] " + GetTimestamp() + " " + message);
        }

        public void LogWarning(string message)
        {
            Log("[WARNING] " + GetTimestamp() + " " + message);
        }

        public void LogError(string message)
        {
            Log("[ERROR] " + GetTimestamp() + " " + message);
        }

        private void Log(string message)
        {
            logQueue.Enqueue(message);
            logEvent.Set(); // 通知写入线程有新日志
        }

        private void WriteLogThread()
        {
            while (!cts.IsCancellationRequested)
            {
                // 等待信号，超时5秒后也检查一次（防止信号丢失）
                logEvent.WaitOne(TimeSpan.FromSeconds(5));

                while (logQueue.TryDequeue(out string logEntry))
                {
                    try
                    {
                        using (FileStream fileStream = new FileStream(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
                        using (StreamWriter streamWriter = new StreamWriter(fileStream))
                        {
                            streamWriter.WriteLine(logEntry);
                        }
                    }
                    catch (IOException)
                    {
                        // 处理 IOException（文件正在使用），重新入队并等待
                        logQueue.Enqueue(logEntry);
                        Thread.Sleep(1000);
                    }
                }

                // 写入所有条目后检查日志大小
                try
                {
                    if (new FileInfo(logFilePath).Length > maxLogSize)
                    {
                        RotateLogFile();
                    }
                }
                catch { /* 忽略文件大小检查异常 */ }
            }
        }


        private void RotateLogFile()
        {
            // 从日志文件中读取所有行
            string[] lines = File.ReadAllLines(logFilePath);

            // 删除前10行
            lines = lines[10..];

            // 将剩余行写回日志文件
            File.WriteAllLines(logFilePath, lines);

            // 在最后追加一条新的日志消息
            File.AppendAllText(logFilePath, "[INFO] " + GetTimestamp() + " Log rotated." + Environment.NewLine);
        }

        private string GetTimestamp()
        {
            return DateTime.Now.ToString("[yyyy-MM-dd HH:mm:ss]");
        }

        public void OpenLogFileDirectory()
        {
            string logFileDirectory = Path.GetDirectoryName(logFilePath);
            if (Directory.Exists(logFileDirectory))
            {
                System.Diagnostics.Process.Start("explorer.exe", logFileDirectory);
            }
            else
            {
                Console.WriteLine("Log file directory does not exist.");
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                disposed = true;
                cts.Cancel();
                logEvent.Set(); // 唤醒写入线程使其退出
                logEvent.Dispose();
                cts.Dispose();
            }
        }
    }
}
