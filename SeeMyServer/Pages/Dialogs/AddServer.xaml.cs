using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Windows.ApplicationModel.Resources;
using Windows.Storage;


namespace SeeMyServer.Pages.Dialogs
{
    public sealed partial class AddServer : ContentDialog
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public CMSModel CMSData { get; private set; }
        public CMSModel IncomingData { get; private set; }
        public bool ManageSSHKeysRequested { get; private set; }
        public string PendingPlainPassword { get; private set; }
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
        private List<SSHKeyModel> sshKeys = new List<SSHKeyModel>();
        private Logger logger;
        public AddServer(CMSModel cmsModel, string pendingPlainPassword = null)
        {
            this.InitializeComponent();

            // 设置日志，最大1MB
            logger = new Logger(1);

            // 将Dialog两个按钮点击事件绑定
            PrimaryButtonClick += MyDialog_PrimaryButtonClick;
            SecondaryButtonClick += MyDialog_SecondaryButtonClick;

            // 初始化Dialog中的字段，使用传入的CMSModel对象的属性
            CMSData = cmsModel;
            DisplayNameTextBox.Text = cmsModel.Name;
            HostIPTextBox.Text = cmsModel.HostIP;
            HostPortTextBox.Text = cmsModel.HostPort;
            SSHUserTextBox.Text = cmsModel.SSHUser;
            if (cmsModel.SSHKeyIsOpen == "True")
            {
                SSHKeyOrPasswdToggleSwitch.IsOn = true;
            }
            else
            {
                SSHKeyOrPasswdToggleSwitch.IsOn = false;
            }
            if (!string.IsNullOrEmpty(pendingPlainPassword))
            {
                SSHPasswd.Password = pendingPlainPassword;
            }
            else if (cmsModel.SSHPasswd != null && cmsModel.SSHPasswd != "")
            {
                SSHPasswd.PlaceholderText = "<Not Changed>";
            }
            logger.LogInfo("Dialog field initialization completed.");

            // 加载SSH密钥列表
            LoadSSHKeys(GetConfiguredSSHKeyId(cmsModel));

            // 添加操作系统类型
            OSTypeComboBox.Items.Add("Linux");

            if (cmsModel.OSType == null)
            {
                OSTypeComboBox.SelectedItem = "Linux";
            }
            else
            {
                OSTypeComboBox.SelectedItem = cmsModel.OSType;
            }

            // 刷新Key Auth状态
            PrivateKeyIsOpen();
        }

        private void MyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SaveFormStateToModel(true);
        }

        // 获取选中内容并转换为字符串
        private string GetSelectedComboBoxItemAsString(ComboBox comboBox)
        {
            if (comboBox.SelectedItem != null)
            {
                // 直接返回选中项作为字符串
                return comboBox.SelectedItem.ToString();
            }
            // 如果没有选中项，则返回空字符串
            return "<Unknown OS>";
        }

        private void MyDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // 在"取消"按钮点击事件中不做任何操作
        }

        private void privateKeyIsOpen_Toggled(object sender, RoutedEventArgs e)
        {
            PrivateKeyIsOpen();
        }
        private void PrivateKeyIsOpen()
        {
            if (SSHKeyOrPasswdToggleSwitch.IsOn == true)
            {
                AddSSHKey.Visibility = Visibility.Visible;
                AddSSHPasswd.Visibility = Visibility.Collapsed;
                SSHKeyTips.Visibility = Visibility.Collapsed;
                SSHPasswdTips.Visibility = Visibility.Collapsed;
            }
            else
            {
                AddSSHKey.Visibility = Visibility.Collapsed;
                AddSSHPasswd.Visibility = Visibility.Visible;
                SSHKeyTips.Visibility = Visibility.Collapsed;
                SSHPasswdTips.Visibility = Visibility.Visible;
            }
            logger.LogInfo("PrivateKeyIsOpen() completed.");
        }

        private async void ImportSSHKey_Click(object sender, RoutedEventArgs e)
        {
            int? sshKeyId = await SSHKeyMethod.ImportKey();
            if (sshKeyId != null)
            {
                LoadSSHKeys(sshKeyId.Value.ToString());
            }
        }

        //private void ConfirmPasteSSHKey_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        int sshKeyId = SSHKeyMethod.SavePrivateKey(SSHKeyNameTextBox.Text, SSHPrivateKeyTextBox.Text);
        //        SSHKeyNameTextBox.Text = "";
        //        SSHPrivateKeyTextBox.Text = "";
        //        PasteSSHKeyError.Visibility = Visibility.Collapsed;
        //        PasteSSHKeyFlyout.Hide();
        //        LoadSSHKeys(sshKeyId.ToString());
        //    }
        //    catch (Exception ex)
        //    {
        //        PasteSSHKeyError.Text = string.Format(resourceLoader.GetString("PasteSSHKeyError"), ex.Message);
        //        PasteSSHKeyError.Visibility = Visibility.Visible;
        //    }
        //}

        private async void DeleteSSHKey_Click(object sender, RoutedEventArgs e)
        {
            // 注意：此按钮在 XAML 中已注释。如需启用，建议改为 Flyout 确认模式
            // （与 ManageSSHKeys 一致），避免 ContentDialog 嵌套问题。
            if (!await WindowsHelloHelper.VerifyAsync(resourceLoader.GetString("WindowsHelloVerifyMessage")))
                return;

            if (SSHKeyComboBox.SelectedItem is SSHKeyModel selectedKey)
            {
                SQLiteHelper dbHelper = new SQLiteHelper();
                dbHelper.DeleteSSHKey(selectedKey.Id);
                LoadSSHKeys("");
            }
        }

        private async void ManageSSHKeysNav_Click(object sender, RoutedEventArgs e)
        {
            // Windows Hello 鉴权
            if (!await WindowsHelloHelper.VerifyAsync(resourceLoader.GetString("WindowsHelloVerifyMessage")))
                return;

            // WinUI 3 不允许在 ContentDialog 内再打开 ContentDialog。
            // 这里仅保存状态并关闭当前对话框，外层页面负责打开密钥管理后再重新打开本对话框。
            PendingPlainPassword = SSHPasswd.Password;
            SaveFormStateToModel(false);
            ManageSSHKeysRequested = true;
            this.Hide();
        }

        /// <summary>
        /// 将当前表单输入保存到 CMSData（不触发 PrimaryButton 的完整保存逻辑）
        /// </summary>
        private void SaveFormStateToModel(bool isPrimary)
        {
            CMSData.Name = string.IsNullOrEmpty(DisplayNameTextBox.Text) ? "<Unnamed>" : DisplayNameTextBox.Text;
            CMSData.HostIP = HostIPTextBox.Text;
            CMSData.HostPort = HostPortTextBox.Text;
            CMSData.SSHUser = SSHUserTextBox.Text;
            CMSData.OSType = GetSelectedComboBoxItemAsString(OSTypeComboBox);

            if (SSHKeyOrPasswdToggleSwitch.IsOn == true)
            {
                CMSData.SSHKeyIsOpen = "True";
                CMSData.SSHKeyId = GetSelectedSSHKeyId();
                CMSData.SSHKey = "";
                CMSData.SSHPasswd = null;
            }
            else
            {
                CMSData.SSHKeyIsOpen = "False";
                CMSData.SSHKeyId = "";
                CMSData.SSHKey = null;

                if (isPrimary && !string.IsNullOrEmpty(SSHPasswd.Password))
                {
                    string key = Method.LoadKeyFromLocalSettings();
                    string iv = Method.LoadIVFromLocalSettings();
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
                    {
                        key = Method.GenerateRandomKey();
                        iv = Method.GenerateRandomIV();
                        Method.SaveKeyToLocalSettings(key);
                        Method.SaveIVToLocalSettings(iv);
                    }

                    SymmetricAlgorithm symmetricAlgorithm = new AesManaged();
                    symmetricAlgorithm.Key = Convert.FromBase64String(key);
                    symmetricAlgorithm.IV = Convert.FromBase64String(iv);
                    string encrypted = Method.EncryptString(SSHPasswd.Password, symmetricAlgorithm);
                    CMSData.SSHPasswd = encrypted;
                }
            }
        }

        private void SSHKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        //    DeleteSSHKeyButton.IsEnabled = SSHKeyComboBox.SelectedItem != null;
        }

        private void LoadSSHKeys(string selectedSSHKeyId)
        {
            SQLiteHelper dbHelper = new SQLiteHelper();
            sshKeys = dbHelper.QuerySSHKeys();
            SSHKeyComboBox.ItemsSource = sshKeys;
            SSHKeyComboBox.SelectedItem = null;

            foreach (SSHKeyModel sshKey in sshKeys)
            {
                if (sshKey.Id.ToString() == selectedSSHKeyId)
                {
                    SSHKeyComboBox.SelectedItem = sshKey;
                    break;
                }
            }

            //DeleteSSHKeyButton.IsEnabled = SSHKeyComboBox.SelectedItem != null;
        }

        private string GetSelectedSSHKeyId()
        {
            if (SSHKeyComboBox.SelectedItem is SSHKeyModel selectedKey)
            {
                return selectedKey.Id.ToString();
            }
            return "";
        }

        private string GetConfiguredSSHKeyId(CMSModel cmsModel)
        {
            if (!string.IsNullOrEmpty(cmsModel.SSHKeyId))
            {
                return cmsModel.SSHKeyId;
            }

            return int.TryParse(cmsModel.SSHKey, out _) ? cmsModel.SSHKey : "";
        }
    }
}
