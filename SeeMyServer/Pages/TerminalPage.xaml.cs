using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Renci.SshNet;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SeeMyServer.Pages
{
    public sealed partial class TerminalPage : Page
    {
        private SshClient _sshClient;
        private ShellStream _shellStream;
        private CancellationTokenSource _readCts;
        private CMSModel _cmsModel;
        private bool _isConnected;
        private bool _pageLoaded;
        private readonly TaskCompletionSource _pageLoadedTcs = new TaskCompletionSource();

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
                PostMessage("str", "Connecting...\r\n");

                if (cmsModel.SSHKeyIsOpen == "True" && !string.IsNullOrEmpty(cmsModel.SSHKeyId))
                {
                    string privateKeyContent = SSHKeyMethod.LoadPrivateKeyFromDB(cmsModel.SSHKeyId);
                    byte[] keyBytes = Encoding.UTF8.GetBytes(privateKeyContent);
                    var keyStream = new MemoryStream(keyBytes, 0, keyBytes.Length, false, true);
                    try
                    {
                        var keyFile = new PrivateKeyFile(keyStream);
                        var connectionInfo = new ConnectionInfo(
                            cmsModel.HostIP,
                            int.Parse(cmsModel.HostPort),
                            cmsModel.SSHUser,
                            new PrivateKeyAuthenticationMethod(cmsModel.SSHUser, keyFile));

                        _sshClient = new SshClient(connectionInfo);
                        await Task.Run(() => _sshClient.Connect());
                    }
                    finally
                    {
                        keyStream.Dispose();
                        Array.Clear(keyBytes, 0, keyBytes.Length);
                        privateKeyContent = null;
                        keyBytes = null;
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                else
                {
                    var connectionInfo = new ConnectionInfo(
                        cmsModel.HostIP,
                        int.Parse(cmsModel.HostPort),
                        cmsModel.SSHUser,
                        new PasswordAuthenticationMethod(cmsModel.SSHUser, cmsModel.SSHPasswd));

                    _sshClient = new SshClient(connectionInfo);
                    await Task.Run(() => _sshClient.Connect());
                }

                _shellStream = _sshClient.CreateShellStream(
                    "xterm-256color",
                    (ushort)80,
                    (ushort)24,
                    0,
                    0,
                    1024,
                    null);

                _isConnected = true;
                _readCts = new CancellationTokenSource();
                ReconnectButton.Visibility = Visibility.Collapsed;

                _ = ReadLoopAsync(_readCts.Token);
            }
            catch (Exception ex)
            {
                PostMessage("str", $"\r\nConnection failed: {ex.Message}\r\n");
                ReconnectButton.Visibility = Visibility.Visible;
                Disconnect();
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
                PostMessage("str", $"\r\n[Disconnected: {ex.Message}]\r\n");
                _isConnected = false;
            }

            if (_isConnected)
            {
                PostMessage("str", "\r\n[Connection closed]\r\n");
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
                Frame.GoBack();
            else
                Frame.Navigate(typeof(HomePage));
        }

        private async void ReconnectButton_Click(object sender, RoutedEventArgs e)
        {
            ReconnectButton.Visibility = Visibility.Collapsed;
            PostMessage("str", "\r\nReconnecting...\r\n");
            await ConnectAsync(_cmsModel);
        }

        private void Disconnect()
        {
            _isConnected = false;
            _readCts?.Cancel();
            _readCts?.Dispose();
            _readCts = null;

            try { _shellStream?.Dispose(); } catch { }
            _shellStream = null;

            try { _sshClient?.Dispose(); } catch { }
            _sshClient = null;
        }
    }
}
