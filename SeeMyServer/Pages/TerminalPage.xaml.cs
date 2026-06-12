using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace SeeMyServer.Pages
{
    public sealed partial class TerminalPage : Page
    {
        private SshClient _sshClient;
        private SftpClient _sftpClient;
        private ShellStream _shellStream;
        private CancellationTokenSource _readCts;
        private CMSModel _cmsModel;
        private bool _isConnected;
        private bool _pageLoaded;
        private readonly TaskCompletionSource _pageLoadedTcs = new TaskCompletionSource();
        private string _currentSftpPath = "/";
        private readonly ObservableCollection<SftpFileItem> _fileItems = new ObservableCollection<SftpFileItem>();
        private bool _sftpReady;
        private readonly ResourceLoader _res = ResourceLoader.GetForViewIndependentUse();

        private const string TerminalHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/xterm@5.3.0/css/xterm.min.css"" />
</head>
<body>
    <div id=""terminal""></div>
    <script src=""https://cdn.jsdelivr.net/npm/xterm@5.3.0/lib/xterm.min.js""></script>
    <script src=""https://cdn.jsdelivr.net/npm/xterm-addon-fit@0.8.0/lib/xterm-addon-fit.min.js""></script>
    <script>
        const term = new Terminal({
            cursorBlink: true,
            fontSize: 14,
            fontFamily: 'Cascadia Mono, Consolas, monospace',
            theme: { background: '#1e1e1e', foreground: '#d4d4d4' },
            allowTransparency: true,
        });
        const fitAddon = new FitAddon.FitAddon();
        term.loadAddon(fitAddon);
        term.open(document.getElementById('terminal'));
        fitAddon.fit();
        setTimeout(() => { try { fitAddon.fit(); } catch(e) {} }, 500);
        term.onData(data => {
            window.chrome.webview.postMessage(JSON.stringify({type:'input',data:data}));
        });
        window.chrome.webview.addEventListener('message', event => {
            try {
                const msg = JSON.parse(event.data);
                if (msg.type === 'output' && msg.d) {
                    const bytes = Uint8Array.from(atob(msg.d), c => c.charCodeAt(0));
                    term.write(bytes);
                } else if (msg.type === 'str') {
                    term.write(msg.d);
                }
            } catch(e) {}
        });
        const ro = new ResizeObserver(() => { try { fitAddon.fit(); } catch(e) {} });
        ro.observe(document.getElementById('terminal'));
    </script>
    <style>
        html, body { height: 100%; margin: 0; padding: 0; background: #1e1e1e; overflow: hidden; }
        #terminal { height: 100%; width: 100%; }
    </style>
</head>
</html>";

        public TerminalPage()
        {
            this.InitializeComponent();
            FileList.ItemsSource = _fileItems;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            _cmsModel = e.Parameter as CMSModel;
            if (_cmsModel == null) return;

            HeaderTextBlock.Text = $"{_cmsModel.SSHUser}@{_cmsModel.HostIP}:{_cmsModel.HostPort}";
            await ConnectAsync(_cmsModel);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            Disconnect();
        }

        private async Task ConnectAsync(CMSModel cmsModel)
        {
            try
            {
                await InitWebViewAsync();
                PostMessage("str", _res.GetString("TerminalConnecting") + "\r\n");

                ConnectionInfo connectionInfo = BuildConnectionInfo(cmsModel);
                if (connectionInfo == null)
                {
                    PostMessage("str", $"\r\n{_res.GetString("TerminalConnectionFailed")}\r\n");
                    ReconnectButton.Visibility = Visibility.Visible;
                    return;
                }

                _sshClient = new SshClient(connectionInfo);
                await Task.Run(() => _sshClient.Connect());

                _shellStream = _sshClient.CreateShellStream(
                    "xterm-256color", (ushort)80, (ushort)24, 0, 0, 1024, null);

                _isConnected = true;
                _readCts = new CancellationTokenSource();
                ReconnectButton.Visibility = Visibility.Collapsed;

                _ = Task.Run(() => InitSftpAsync(connectionInfo));
                _ = ReadLoopAsync(_readCts.Token);
            }
            catch (Exception ex)
            {
                PostMessage("str", $"\r\n{string.Format(_res.GetString("TerminalConnectionFailed"), ex.Message)}\r\n");
                ReconnectButton.Visibility = Visibility.Visible;
                Disconnect();
            }
        }

        private ConnectionInfo BuildConnectionInfo(CMSModel cmsModel)
        {
            int port = int.TryParse(cmsModel.HostPort, out int p) ? p : 22;

            if (cmsModel.SSHKeyIsOpen == "True" && !string.IsNullOrEmpty(cmsModel.SSHKeyId))
            {
                string privateKeyContent = SSHKeyMethod.LoadPrivateKeyFromDB(cmsModel.SSHKeyId);
                byte[] keyBytes = Encoding.UTF8.GetBytes(privateKeyContent);
                var keyStream = new MemoryStream(keyBytes, 0, keyBytes.Length, false, true);
                try
                {
                    var keyFile = new PrivateKeyFile(keyStream);
                    return new ConnectionInfo(
                        cmsModel.HostIP, port, cmsModel.SSHUser,
                        new PrivateKeyAuthenticationMethod(cmsModel.SSHUser, keyFile));
                }
                finally
                {
                    keyStream.Dispose();
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                    privateKeyContent = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }
            }

            return new ConnectionInfo(
                cmsModel.HostIP, port, cmsModel.SSHUser,
                new PasswordAuthenticationMethod(cmsModel.SSHUser, cmsModel.SSHPasswd));
        }

        private async Task InitSftpAsync(ConnectionInfo connectionInfo)
        {
            try
            {
                var sftp = new SftpClient(connectionInfo);
                sftp.Connect();
                _sftpClient = sftp;
                _sftpReady = true;

                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    SftpPanel.Visibility = Visibility.Visible;
                    SftpStatusText.Text = _res.GetString("SftpConnected");
                    SftpStatusText.Visibility = Visibility.Visible;
                    _ = RefreshFileListAsync();
                });
            }
            catch (Exception ex)
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    SftpStatusText.Text = string.Format(_res.GetString("SftpError"), ex.Message);
                    SftpStatusText.Visibility = Visibility.Visible;
                });
            }
        }

        private async Task RefreshFileListAsync()
        {
            if (!_sftpReady || _sftpClient == null) return;

            try
            {
                var files = await Task.Run(() =>
                {
                    return _sftpClient.ListDirectory(_currentSftpPath)
                        .Where(f => f.Name != "." && f.Name != "..")
                        .Select(f => new SftpFileItem(f))
                        .OrderByDescending(f => f.IsDirectory)
                        .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                });

                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    _fileItems.Clear();
                    foreach (var f in files) _fileItems.Add(f);
                    PathBox.Text = _currentSftpPath;
                    FileCountText.Text = string.Format(_res.GetString("SftpItemsCount"), files.Count);
                });
            }
            catch (Exception ex)
            {
                _ = DispatcherQueue.TryEnqueue(() =>
                {
                    SftpStatusText.Text = string.Format(_res.GetString("SftpError"), ex.Message);
                });
            }
        }

        private async Task GoToDirectoryAsync(string path)
        {
            _currentSftpPath = NormalizePath(path);
            await RefreshFileListAsync();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            path = path.Replace('\\', '/');
            if (!path.StartsWith("/")) path = "/" + path;
            if (path.Length > 1 && path.EndsWith("/")) path = path.TrimEnd('/');
            return path;
        }

        private async void GoUpButton_Click(object sender, RoutedEventArgs e)
        {
            var parent = GetParentPath(_currentSftpPath);
            await GoToDirectoryAsync(parent);
        }

        private static string GetParentPath(string path)
        {
            if (path == "/") return "/";
            int idx = path.LastIndexOf('/');
            if (idx <= 0) return "/";
            return path.Substring(0, idx);
        }

        private async void PathBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await GoToDirectoryAsync(PathBox.Text);
            }
        }

        private async void FileList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            var item = FileList.SelectedItem as SftpFileItem;
            if (item == null) return;

            if (item.IsDirectory)
            {
                await GoToDirectoryAsync(item.FullPath);
            }
        }

        private void FileList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var item = (e.OriginalSource as FrameworkElement)?.DataContext as SftpFileItem;
            if (item == null) return;
            FileList.SelectedItem = item;

            var flyout = new MenuFlyout();
            if (item.IsDirectory)
            {
                var openItem = new MenuFlyoutItem { Text = _res.GetString("SftpOpen") };
                openItem.Click += async (s, a) => await GoToDirectoryAsync(item.FullPath);
                flyout.Items.Add(openItem);
            }
            else
            {
                var downloadItem = new MenuFlyoutItem { Text = _res.GetString("SftpDownload") };
                downloadItem.Click += async (s, a) => await DownloadFileAsync(item);
                flyout.Items.Add(downloadItem);
            }

            var renameItem = new MenuFlyoutItem { Text = _res.GetString("SftpRename") };
            renameItem.Click += async (s, a) => await RenameItemAsync(item);
            flyout.Items.Add(renameItem);

            var deleteItem = new MenuFlyoutItem { Text = _res.GetString("SftpDelete") };
            deleteItem.Click += async (s, a) => await DeleteItemAsync(item);
            flyout.Items.Add(deleteItem);

            flyout.ShowAt(FileList, e.GetPosition(FileList));
        }

        private async void UploadButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(App.m_window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hWnd);
            picker.FileTypeFilter.Add(".");

            var files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;

            TransferProgress.Visibility = Visibility.Visible;
            TransferProgress.Value = 0;
            TransferProgress.IsIndeterminate = false;
            SftpStatusText.Text = string.Format(_res.GetString("SftpUploadingProgress"), 0, files.Count, "", 0);
            int ok = 0, fail = 0;
            int fileIdx = 0;

            foreach (var file in files)
            {
                fileIdx++;
                try
                {
                    var buffer = await FileIO.ReadBufferAsync(file);
                    var bytes = new byte[buffer.Length];
                    using (var reader = global::Windows.Storage.Streams.DataReader.FromBuffer(buffer))
                    {
                        reader.ReadBytes(bytes);
                    }
                    string remotePath = _currentSftpPath + "/" + file.Name;
                    long totalBytes = bytes.Length;
                    int lastPct = -1;
                    await Task.Run(() =>
                    {
                        using (var ms = new MemoryStream(bytes))
                            _sftpClient.UploadFile(ms, remotePath, bytesWritten =>
                            {
                                int pct = totalBytes > 0 ? (int)(bytesWritten * 100 / (ulong)totalBytes) : 0;
                                if (pct != lastPct && pct % 5 == 0)
                                {
                                    lastPct = pct;
                                    _ = DispatcherQueue.TryEnqueue(() =>
                                    {
                                        TransferProgress.Value = pct;
                                        SftpStatusText.Text = string.Format(_res.GetString("SftpUploadingProgress"), fileIdx, files.Count, file.Name, pct);
                                    });
                                }
                            });
                    });
                    ok++;
                }
                catch
                {
                    fail++;
                }
            }

            SftpStatusText.Text = string.Format(_res.GetString("SftpUploaded"), ok, fail);
            TransferProgress.Visibility = Visibility.Collapsed;
            await RefreshFileListAsync();
        }

        private static readonly Guid FOLDERID_Downloads = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHGetKnownFolderPath(Guid rfid, uint dwFlags, IntPtr hToken, out string pszPath);

        private static string GetDownloadsFolder()
        {
            SHGetKnownFolderPath(FOLDERID_Downloads, 0, IntPtr.Zero, out string path);
            return path;
        }

        private async Task DownloadFileAsync(SftpFileItem item)
        {
            string folder = GetDownloadsFolder();
            string path = Path.Combine(folder, item.Name);

            // avoid overwriting existing files
            int copy = 1;
            string basePath = path;
            while (File.Exists(path))
            {
                string name = Path.GetFileNameWithoutExtension(item.Name);
                string ext = Path.GetExtension(item.Name);
                path = Path.Combine(folder, $"{name} ({copy}){ext}");
                copy++;
            }

            TransferProgress.Visibility = Visibility.Visible;
            TransferProgress.Value = 0;
            TransferProgress.IsIndeterminate = false;
            SftpStatusText.Text = string.Format(_res.GetString("SftpDownloadingStart"), item.Name);

            long fileSize = item.Size;
            int lastPct = -1;

            try
            {
                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                using (var ms = new MemoryStream())
                {
                    await Task.Run(() =>
                    {
                        _sftpClient.DownloadFile(item.FullPath, ms, bytesRead =>
                        {
                            int pct = fileSize > 0 ? (int)(bytesRead * 100 / (ulong)fileSize) : 0;
                            if (pct != lastPct && pct % 5 == 0)
                            {
                                lastPct = pct;
                                _ = DispatcherQueue.TryEnqueue(() =>
                                {
                                    TransferProgress.Value = pct;
                                    SftpStatusText.Text = string.Format(_res.GetString("SftpDownloadingProgress"), item.Name, pct);
                                });
                            }
                        });
                    });
                    ms.Position = 0;
                    await ms.CopyToAsync(fs);
                }
                SftpStatusText.Text = string.Format(_res.GetString("SftpDownloaded"), Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                SftpStatusText.Text = string.Format(_res.GetString("SftpDownloadFailed"), ex.Message);
            }
            finally
            {
                TransferProgress.Visibility = Visibility.Collapsed;
            }
        }

        private async Task DeleteItemAsync(SftpFileItem item)
        {
            var dialog = new ContentDialog
            {
                Title = _res.GetString("SftpDeleteConfirmTitle"),
                Content = string.Format(_res.GetString("SftpDeleteConfirmContent"), item.Name),
                PrimaryButtonText = _res.GetString("SftpDelete"),
                CloseButtonText = _res.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            try
            {
                await Task.Run(() =>
                {
                    if (item.IsDirectory)
                        _sftpClient.DeleteDirectory(item.FullPath);
                    else
                        _sftpClient.DeleteFile(item.FullPath);
                });
                await RefreshFileListAsync();
            }
            catch (Exception ex)
            {
                SftpStatusText.Text = $"Delete failed: {ex.Message}";
            }
        }

        private async Task RenameItemAsync(SftpFileItem item)
        {
            var renameBox = new TextBox
            {
                Text = item.Name,
                FontFamily = new FontFamily("Consolas"),
            };
            renameBox.GotFocus += (s, e) => renameBox.SelectAll();
            var dialog = new ContentDialog
            {
                Title = _res.GetString("SftpRenameTitle"),
                Content = renameBox,
                PrimaryButtonText = _res.GetString("SftpRename"),
                CloseButtonText = _res.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            renameBox.Focus(FocusState.Programmatic);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var newName = (renameBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(newName) || newName == item.Name) return;

            try
            {
                string newPath = GetParentPath(item.FullPath) + "/" + newName;
                await Task.Run(() => _sftpClient.RenameFile(item.FullPath, newPath));
                await RefreshFileListAsync();
            }
            catch (Exception ex)
            {
                SftpStatusText.Text = $"Rename failed: {ex.Message}";
            }
        }

        private async void NewFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var folderBox = new TextBox
            {
                Text = "new_folder",
                FontFamily = new FontFamily("Consolas"),
            };
            folderBox.GotFocus += (s, e) => folderBox.SelectAll();
            var dialog = new ContentDialog
            {
                Title = _res.GetString("SftpNewFolderTitle"),
                Content = folderBox,
                PrimaryButtonText = _res.GetString("SftpCreate"),
                CloseButtonText = _res.GetString("Cancel"),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
            };

            folderBox.Focus(FocusState.Programmatic);
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var name = (folderBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;

            try
            {
                string newPath = _currentSftpPath + "/" + name;
                await Task.Run(() => _sftpClient.CreateDirectory(newPath));
                await RefreshFileListAsync();
            }
            catch (Exception ex)
            {
                SftpStatusText.Text = $"Create folder failed: {ex.Message}";
            }
        }

        private async Task InitWebViewAsync()
        {
            await TerminalView.EnsureCoreWebView2Async();
            var core = TerminalView.CoreWebView2;
            core.Settings.IsScriptEnabled = true;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.IsWebMessageEnabled = true;
            core.WebMessageReceived += OnWebMessageReceived;
            core.NavigationCompleted += (s, e) => { _pageLoaded = true; _pageLoadedTcs.TrySetResult(); };
            core.NavigateToString(TerminalHtml);
            await Task.WhenAny(_pageLoadedTcs.Task, Task.Delay(15000));
        }

        private void OnWebMessageReceived(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs args)
        {
            try
            {
                string raw = args.TryGetWebMessageAsString();
                if (string.IsNullOrEmpty(raw)) return;
                var doc = System.Text.Json.JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("type", out var t) && t.GetString() == "input" &&
                    root.TryGetProperty("data", out var d) && _isConnected && _shellStream != null)
                {
                    string input = d.GetString() ?? "";
                    byte[] bytes = Encoding.UTF8.GetBytes(input);
                    _shellStream.Write(bytes, 0, bytes.Length);
                    _shellStream.Flush();
                }
                doc.Dispose();
            }
            catch { }
        }

        private void PostMessage(string type, string data)
        {
            if (!_pageLoaded || TerminalView.CoreWebView2 == null) return;
            try
            {
                string json = "{\"type\":\"" + type + "\",\"d\":\"" + JsonEscape(data) + "\"}";
                TerminalView.CoreWebView2.PostWebMessageAsString(json);
            }
            catch { }
        }

        private void PostBase64(byte[] buffer, int count)
        {
            if (!_pageLoaded || TerminalView.CoreWebView2 == null) return;
            try
            {
                string b64 = Convert.ToBase64String(buffer, 0, count);
                TerminalView.CoreWebView2.PostWebMessageAsString("{\"type\":\"output\",\"d\":\"" + b64 + "\"}");
            }
            catch { }
        }

        private async Task ReadLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[32768];
            try
            {
                while (!ct.IsCancellationRequested && _isConnected)
                {
                    int bytesRead = await Task.Run(() => _shellStream.Read(buffer, 0, buffer.Length), ct);
                    if (bytesRead <= 0) break;
                    PostBase64(buffer, bytesRead);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                PostMessage("str", $"\r\n{string.Format(_res.GetString("TerminalDisconnected"), ex.Message)}\r\n");
                _isConnected = false;
            }

            if (_isConnected)
            {
                PostMessage("str", $"\r\n{_res.GetString("TerminalConnectionClosed")}\r\n");
                Disconnect();
            }

            _ = DispatcherQueue.TryEnqueue(() => ReconnectButton.Visibility = Visibility.Visible);
        }

        private static string JsonEscape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                    .Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var item = FileList.SelectedItem as SftpFileItem;
            if (item != null) await DeleteItemAsync(item);
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            else Frame.Navigate(typeof(HomePage));
        }

        private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
        {
            ReconnectButton.Visibility = Visibility.Collapsed;
            PostMessage("str", "\r\n" + _res.GetString("TerminalReconnecting") + "\r\n");
            await ConnectAsync(_cmsModel);
        }

        private void Disconnect()
        {
            _isConnected = false;
            _sftpReady = false;
            _readCts?.Cancel();
            _readCts?.Dispose();
            _readCts = null;

            try { _shellStream?.Dispose(); } catch { }
            _shellStream = null;

            try { _sftpClient?.Dispose(); } catch { }
            _sftpClient = null;

            try { _sshClient?.Dispose(); } catch { }
            _sshClient = null;
        }
    }

    public class SftpFileItem
    {
        public string Name { get; }
        public string FullPath { get; }
        public bool IsDirectory { get; }
        public long Size { get; }
        public string SizeDisplay { get; }
        public string ModifiedDisplay { get; }
        public string Icon { get; }

        public SftpFileItem(ISftpFile file)
        {
            Name = file.Name;
            FullPath = file.FullName;
            IsDirectory = file.IsDirectory;
            Size = file.Length;
            Icon = file.IsDirectory ? "\U0001F4C1" : "\U0001F4C4";

            if (file.IsDirectory)
                SizeDisplay = "<DIR>";
            else if (file.Length < 1024)
                SizeDisplay = $"{file.Length} B";
            else if (file.Length < 1024 * 1024)
                SizeDisplay = $"{file.Length / 1024.0:F1} KB";
            else if (file.Length < 1024 * 1024 * 1024)
                SizeDisplay = $"{file.Length / (1024.0 * 1024):F1} MB";
            else
                SizeDisplay = $"{file.Length / (1024.0 * 1024 * 1024):F1} GB";

            ModifiedDisplay = file.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
        }
    }
}
