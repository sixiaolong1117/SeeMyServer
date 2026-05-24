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
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();
        private List<SSHKeyModel> sshKeys = new List<SSHKeyModel>();
        private Logger logger;
        public AddServer(CMSModel cmsModel)
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
            if (cmsModel.SSHPasswd != null && cmsModel.SSHPasswd != "")
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

        private void DeleteSSHKey_Click(object sender, RoutedEventArgs e)
        {
            if (SSHKeyComboBox.SelectedItem is SSHKeyModel selectedKey)
            {
                SQLiteHelper dbHelper = new SQLiteHelper();
                dbHelper.DeleteSSHKey(selectedKey.Id);
                LoadSSHKeys("");
            }
        }

        private async void ManageSSHKeysNav_Click(object sender, RoutedEventArgs e)
        {
            // WinUI 3 不允许在 ContentDialog 内再打开 ContentDialog
            // 方案：关闭当前对话框 → 打开管理密钥 → 重新打开当前对话框（保留状态）

            // 1. 保存当前表单输入到 CMSData
            SaveFormStateToModel(false);

            // 2. 关闭当前 AddServer（调用方 HomePage 会收到 None，不执行保存）
            this.Hide();

            // 3. 打开管理密钥对话框（在主窗口 XamlRoot 上）
            ManageSSHKeys keyDialog = new ManageSSHKeys();
            keyDialog.XamlRoot = App.m_window.Content.XamlRoot;
            keyDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            keyDialog.CloseButtonText = resourceLoader.GetString("Cancel");
            await keyDialog.ShowAsync();

            // 4. 重新创建 AddServer 对话框，恢复之前的状态
            AddServer newDialog = new AddServer(CMSData);
            newDialog.XamlRoot = App.m_window.Content.XamlRoot;
            newDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            newDialog.PrimaryButtonText = resourceLoader.GetString(
                CMSData.Id == 0 ? "DialogAdd" : "DialogChange");
            newDialog.CloseButtonText = resourceLoader.GetString("DialogClose");
            newDialog.DefaultButton = ContentDialogButton.Primary;

            ContentDialogResult result = await newDialog.ShowAsync();

            // 5. 用户确认后直接保存并刷新首页
            if (result == ContentDialogResult.Primary)
            {
                SQLiteHelper dbHelper = new SQLiteHelper();
                if (CMSData.Id == 0)
                {
                    CMSData.Id = dbHelper.InsertData(CMSData);
                }
                else
                {
                    dbHelper.UpdateData(CMSData);
                }
                // 导航回首页触发刷新
                App.m_window.NavigateToPage(typeof(HomePage), null);
            }
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
            else if (isPrimary && SSHPasswd.Password != "" && SSHPasswd.Password != null)
            {
                // 仅在 PrimaryButton 按下时加密密码（MyDialog_PrimaryButtonClick 的原有逻辑）
                CMSData.SSHKeyIsOpen = "False";

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
                CMSData.SSHKeyId = "";
                CMSData.SSHKey = null;
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
