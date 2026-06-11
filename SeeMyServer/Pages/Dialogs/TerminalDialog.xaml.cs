using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Renci.SshNet;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SeeMyServer.Pages.Dialogs
{
    public sealed partial class TerminalDialog : ContentDialog
    {
        private SshClient _sshClient;
        private ShellStream _shellStream;
        private readonly SynchronizationContext _syncContext;
        private CancellationTokenSource _readCts;
        private bool _isConnected;

        public TerminalDialog(CMSModel cmsModel)
        {
            this.InitializeComponent();
            _syncContext = SynchronizationContext.Current;

            HeaderTextBlock.Text = $"{cmsModel.SSHUser}@{cmsModel.HostIP}:{cmsModel.HostPort}";
            Loaded += async (s, e) => await ConnectAsync(cmsModel);
            Closed += (s, e) => Disconnect();
        }

        private async Task ConnectAsync(CMSModel cmsModel)
        {
            InputBox.IsEnabled = false;
            SendButton.IsEnabled = false;
            AppendOutput("Connecting...\n");

            try
            {
                var connectionInfo = new ConnectionInfo(
                    cmsModel.HostIP,
                    int.Parse(cmsModel.HostPort),
                    cmsModel.SSHUser);

                if (cmsModel.SSHKeyIsOpen == "True" && !string.IsNullOrEmpty(cmsModel.SSHKeyId))
                {
                    string privateKeyContent = SSHKeyMethod.LoadPrivateKeyFromDB(cmsModel.SSHKeyId);
                    byte[] keyBytes = Encoding.UTF8.GetBytes(privateKeyContent);
                    var keyStream = new MemoryStream(keyBytes, 0, keyBytes.Length, false, true);
                    try
                    {
                        var keyFile = new PrivateKeyFile(keyStream);
                        connectionInfo.AuthenticationMethods.Clear();
                        connectionInfo.AuthenticationMethods.Add(
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
                    connectionInfo.AuthenticationMethods.Clear();
                    connectionInfo.AuthenticationMethods.Add(
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

                InputBox.IsEnabled = true;
                SendButton.IsEnabled = true;
                InputBox.Focus(FocusState.Programmatic);
                AppendOutput("Connected.\n\n");

                _ = ReadOutputLoopAsync(_readCts.Token);
            }
            catch (Exception ex)
            {
                AppendOutput($"\nConnection failed: {ex.Message}\n");
                Disconnect();
            }
        }

        private async Task ReadOutputLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[4096];
            try
            {
                while (!ct.IsCancellationRequested && _isConnected)
                {
                    int bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length, ct);
                    if (bytesRead == 0)
                    {
                        break;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    _syncContext.Post(_ => AppendOutput(text), null);
                }
            }
            catch (OperationCanceledException) { }
            catch { }

            if (_isConnected)
            {
                _syncContext.Post(_ =>
                {
                    AppendOutput("\n[Connection closed]\n");
                    Disconnect();
                }, null);
            }
        }

        private void AppendOutput(string text)
        {
            OutputTextBlock.Text += text;
            OutputScroller.ScrollToVerticalOffset(OutputScroller.ExtentHeight);
        }

        private async void InputBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await SendInputAsync();
                e.Handled = true;
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            await SendInputAsync();
        }

        private async Task SendInputAsync()
        {
            if (!_isConnected || _shellStream == null)
            {
                return;
            }

            string command = InputBox.Text;
            if (string.IsNullOrEmpty(command))
            {
                return;
            }

            try
            {
                _shellStream.WriteLine(command);
                AppendOutput(command + "\n");
                InputBox.Text = "";
            }
            catch
            {
                AppendOutput("\n[Send failed - connection lost]\n");
                Disconnect();
            }
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

            InputBox.IsEnabled = false;
            SendButton.IsEnabled = false;
        }
    }
}
