using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.Resources;
using Windows.Globalization;
using Windows.Storage;
using SeeMyServer.Pages.Dialogs;
using SeeMyServer.Helper;

namespace SeeMyServer.Pages
{
    public sealed partial class SettingsPage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;

        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForViewIndependentUse();

        public SettingsPage()
        {
            this.InitializeComponent();

            InitializeLosesFocus();
            materialStatusSet();
            languageStatusSet();
            WindowsHelloStatusSet();
        }
        // 材料ComboBox列表List
        public List<string> material { get; } = new List<string>()
        {
            "Mica",
            "Mica Alt",
            "Acrylic"
        };

        public List<string> language { get; } = new List<string>()
        {
            "简体中文",
            "English"
        };

        // 初始化 losesFocus 列表
        public List<string> losesFocus { get; } = new List<string>(); 
        private void InitializeLosesFocus()
        {
            losesFocus.Add(resourceLoader.GetString("LosesFocusStopSSH1"));
            //losesFocus.Add(resourceLoader.GetString("LosesFocusStopSSH2"));
            losesFocus.Add(resourceLoader.GetString("LosesFocusStopSSH3"));

            // 读取 LocalSettings 中的选中序号
            if (localSettings.Values.ContainsKey("LosesFocusStopSSHSelectedIndex"))
            {
                // 如果有保存的序号，设置为选中项
                LosesFocusStopSSHComboBox.SelectedIndex = (int)localSettings.Values["LosesFocusStopSSHSelectedIndex"];
            }
            else
            {
                // 如果没有保存的序号，默认选择
                LosesFocusStopSSHComboBox.SelectedIndex = 0;
            }
        }

        private void languageStatusSet()
        {
            if (!languageStatusSetList())
            {
                // 未设置
                localSettings.Values["languageChange"] = Windows.Globalization.Language.CurrentInputMethodLanguageTag;
                languageStatusSetList();
            }
        }
        private bool languageStatusSetList()
        {
            // 读取本地设置数据，调整ComboBox状态
            string languageChangeValue = localSettings.Values["languageChange"] as string;

            switch (languageChangeValue)
            {
                case "zh-Hans-CN":
                    languageChange.SelectedItem = language[0];
                    return true;
                case "en-US":
                    languageChange.SelectedItem = language[1];
                    return true;
                default:
                    return false;
            }
        }
        private void materialStatusSet()
        {
            // 读取本地设置数据，调整ComboBox状态
            string materialStatus = localSettings.Values["materialStatus"] as string;
            switch (materialStatus)
            {
                case "Mica":
                    localSettings.Values["materialStatus"] = "Mica";
                    backgroundMaterial.SelectedItem = material[0];
                    break;
                case "Mica Alt":
                    localSettings.Values["materialStatus"] = "Mica Alt";
                    backgroundMaterial.SelectedItem = material[1];
                    break;
                case "Acrylic":
                    localSettings.Values["materialStatus"] = "Acrylic";
                    backgroundMaterial.SelectedItem = material[2];
                    break;
                default:
                    localSettings.Values["materialStatus"] = "Mica Alt";
                    backgroundMaterial.SelectedItem = material[1];
                    break;
            }
        }


        // 背景材料设置ComboBox改动事件
        private void backgroundMaterial_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string materialStatus = e.AddedItems[0].ToString();
            switch (materialStatus)
            {
                case "Mica":
                    if (localSettings.Values["materialStatus"] as string != "Mica")
                    {
                        localSettings.Values["materialStatus"] = "Mica";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Mica";
                    }
                    break;
                case "Mica Alt":
                default:
                    if (localSettings.Values["materialStatus"] as string != "Mica Alt")
                    {
                        localSettings.Values["materialStatus"] = "Mica Alt";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Mica Alt";
                    }
                    break;
                case "Acrylic":
                    if (localSettings.Values["materialStatus"] as string != "Acrylic")
                    {
                        localSettings.Values["materialStatus"] = "Acrylic";
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["materialStatus"] = "Acrylic";
                    }
                    break;
            }
        }

        private void languageChange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string languageStatus = e.AddedItems[0].ToString();
            switch (languageStatus)
            {
                case "简体中文":
                    if (localSettings.Values["languageChange"] as string != "zh-Hans-CN")
                    {
                        localSettings.Values["languageChange"] = "zh-Hans-CN";
                        ApplicationLanguages.PrimaryLanguageOverride = localSettings.Values["languageChange"] as string;
                        Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", localSettings.Values["languageChange"] as string);
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["languageChange"] = "zh-Hans-CN";
                    }
                    break;
                case "English":
                default:
                    if (localSettings.Values["languageChange"] as string != "en-US")
                    {
                        localSettings.Values["languageChange"] = "en-US";
                        ApplicationLanguages.PrimaryLanguageOverride = localSettings.Values["languageChange"] as string;
                        Windows.ApplicationModel.Resources.Core.ResourceContext.SetGlobalQualifierValue("Language", localSettings.Values["languageChange"] as string);
                        Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                    }
                    else
                    {
                        localSettings.Values["languageChange"] = "en-US";
                    }
                    break;
            }
        }

        private void LosesFocusStopSSHComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LosesFocusStopSSHComboBox.SelectedIndex != -1)
            {
                // 保存选中的序号到 LocalSettings
                localSettings.Values["LosesFocusStopSSHSelectedIndex"] = LosesFocusStopSSHComboBox.SelectedIndex;
            }
        }

        private async void ManageSSHKeysButton_Click(object sender, RoutedEventArgs e)
        {
            if (!await WindowsHelloHelper.VerifyAsync(resourceLoader.GetString("WindowsHelloVerifyMessage")))
                return;

            ManageSSHKeys dialog = new ManageSSHKeys();
            dialog.XamlRoot = this.XamlRoot;
            dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            dialog.CloseButtonText = resourceLoader.GetString("Cancel");
            await dialog.ShowAsync();
        }

        private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Instance.OpenLogFileDirectory();
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            Logger.Instance.ClearLog();
        }

        private void WindowsHelloStatusSet()
        {
            // 初始化 ToggleSwitch 状态，先解绑事件防止触发
            WindowsHelloToggle.Toggled -= WindowsHelloToggle_Toggled;
            WindowsHelloToggle.IsOn = WindowsHelloHelper.IsEnabled;
            WindowsHelloToggle.Toggled += WindowsHelloToggle_Toggled;
        }

        private async void WindowsHelloToggle_Toggled(object sender, RoutedEventArgs e)
        {
            // 防止初始化时触发
            if (WindowsHelloToggle == null)
                return;

            bool targetState = WindowsHelloToggle.IsOn;

            // 先检查设备是否支持
            if (!await WindowsHelloHelper.IsAvailableAsync())
            {
                WindowsHelloToggle.Toggled -= WindowsHelloToggle_Toggled;
                WindowsHelloToggle.IsOn = WindowsHelloHelper.IsEnabled;
                WindowsHelloToggle.Toggled += WindowsHelloToggle_Toggled;
                return;
            }

            if (targetState && !WindowsHelloHelper.IsEnabled)
            {
                // 用户想要开启 → 验证身份
                bool success = await WindowsHelloHelper.EnableAsync(
                    resourceLoader.GetString("WindowsHelloVerifyMessage"));
                if (!success)
                {
                    // 验证失败，恢复关闭状态
                    WindowsHelloToggle.Toggled -= WindowsHelloToggle_Toggled;
                    WindowsHelloToggle.IsOn = false;
                    WindowsHelloToggle.Toggled += WindowsHelloToggle_Toggled;
                }
            }
            else if (!targetState && WindowsHelloHelper.IsEnabled)
            {
                // 用户想要关闭 → 验证身份
                bool success = await WindowsHelloHelper.DisableAsync(
                    resourceLoader.GetString("WindowsHelloVerifyMessage"));
                if (!success)
                {
                    // 验证失败，恢复开启状态
                    WindowsHelloToggle.Toggled -= WindowsHelloToggle_Toggled;
                    WindowsHelloToggle.IsOn = true;
                    WindowsHelloToggle.Toggled += WindowsHelloToggle_Toggled;
                }
            }
        }
    }
}
