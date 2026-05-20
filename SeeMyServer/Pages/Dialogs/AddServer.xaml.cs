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
            // 在"确定"按钮点击事件中保存用户输入的内容
            CMSData.Name = string.IsNullOrEmpty(DisplayNameTextBox.Text) ? "<Unnamed>" : DisplayNameTextBox.Text;
            CMSData.HostIP = HostIPTextBox.Text;
            CMSData.HostPort = HostPortTextBox.Text;
            CMSData.SSHUser = SSHUserTextBox.Text;
            CMSData.OSType = GetSelectedComboBoxItemAsString(OSTypeComboBox);

            // 根据Key Auth状态写入
            if (SSHKeyOrPasswdToggleSwitch.IsOn == true)
            {
                CMSData.SSHKeyIsOpen = "True";
                CMSData.SSHKeyId = GetSelectedSSHKeyId();
                CMSData.SSHKey = "";
                CMSData.SSHPasswd = null;
            }
            else
            {
                if (SSHPasswd.Password != "" && SSHPasswd.Password != null)
                {
                    CMSData.SSHKeyIsOpen = "False";

                    // 检查是否已经存在密钥和初始化向量，如果不存在则生成新的
                    string key = Method.LoadKeyFromLocalSettings();
                    string iv = Method.LoadIVFromLocalSettings();

                    // 如果不存在密钥和初始化向量，则生成新的
                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
                    {
                        key = Method.GenerateRandomKey();
                        iv = Method.GenerateRandomIV();

                        // 将新生成的密钥和初始化向量保存到 localSettings 中
                        Method.SaveKeyToLocalSettings(key);
                        Method.SaveIVToLocalSettings(iv);
                    }

                    // 使用的对称加密算法
                    SymmetricAlgorithm symmetricAlgorithm = new AesManaged();

                    // 设置加密密钥和初始化向量
                    symmetricAlgorithm.Key = Convert.FromBase64String(key);
                    symmetricAlgorithm.IV = Convert.FromBase64String(iv);

                    // 加密字符串
                    string encrypted = Method.EncryptString(SSHPasswd.Password, symmetricAlgorithm);

                    CMSData.SSHPasswd = encrypted;
                    CMSData.SSHKeyId = "";
                    CMSData.SSHKey = null;
                }
            }
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

        private void ConfirmPasteSSHKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int sshKeyId = SSHKeyMethod.SavePrivateKey(SSHKeyNameTextBox.Text, SSHPrivateKeyTextBox.Text);
                SSHKeyNameTextBox.Text = "";
                SSHPrivateKeyTextBox.Text = "";
                PasteSSHKeyError.Visibility = Visibility.Collapsed;
                PasteSSHKeyFlyout.Hide();
                LoadSSHKeys(sshKeyId.ToString());
            }
            catch (Exception ex)
            {
                PasteSSHKeyError.Text = string.Format(resourceLoader.GetString("PasteSSHKeyError"), ex.Message);
                PasteSSHKeyError.Visibility = Visibility.Visible;
            }
        }

        private void DeleteSSHKey_Click(object sender, RoutedEventArgs e)
        {
            if (SSHKeyComboBox.SelectedItem is SSHKeyModel selectedKey)
            {
                SQLiteHelper dbHelper = new SQLiteHelper();
                dbHelper.DeleteSSHKey(selectedKey.Id);
                LoadSSHKeys("");
            }
        }

        private void SSHKeyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DeleteSSHKeyButton.IsEnabled = SSHKeyComboBox.SelectedItem != null;
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

            DeleteSSHKeyButton.IsEnabled = SSHKeyComboBox.SelectedItem != null;
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
