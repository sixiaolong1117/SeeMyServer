using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Devices.Geolocation;
using Windows.Storage;
using Windows.System;

namespace SeeMyServer.Pages
{
    public sealed partial class DetailPage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherTimer timer;
        CMSModel dataList;

        public DetailPage()
        {
            this.InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            dataList = (CMSModel)e.Parameter;
            base.OnNavigatedTo(e);

            LoadData();

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;
        }

        private List<ProgressBar> CreateProgressBars(Grid container, string[] CPUCoreUsageTokens, string CPUCoreNum)
        {
            int numberOfBars = int.Parse(CPUCoreNum);

            // 清除 Grid 的行定义和子元素
            container.RowDefinitions.Clear();
            container.Children.Clear();

            // 检查是否需要添加列定义
            if (container.ColumnDefinitions.Count == 0)
            {
                // 创建一个ColumnDefinition
                ColumnDefinition columnDefinition = new ColumnDefinition();

                // 设置宽度为自动调整大小以填充剩余空间
                columnDefinition.Width = new GridLength(1, GridUnitType.Star);

                // 将ColumnDefinition添加到Grid的ColumnDefinitions集合中
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(50) });
                container.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(45) });
                container.ColumnDefinitions.Add(columnDefinition);
            }

            List<ProgressBar> progressBars = new List<ProgressBar>();

            for (int i = 0; i < numberOfBars; i++)
            {
                // 添加新的行定义
                container.RowDefinitions.Add(new RowDefinition());

                ProgressBar progressBar = new ProgressBar();

                progressBar.Margin = new Thickness(0, 4, 0, 4);

                // 设置ProgressBar的前景色为指定的SolidColorBrush对象
                progressBar.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x42, 0xCD, 0xEF));

                try
                {
                    progressBar.Value = double.Parse(CPUCoreUsageTokens[i]);
                }
                catch (Exception ex) { }

                // 创建 TextBlock 来显示与 ProgressBar 同步的值
                TextBlock textBlock = new TextBlock();
                TextBlock textCPUBlock = new TextBlock();
                textBlock.Text = $"{progressBar.Value:F0}%";
                textCPUBlock.Text = $"CPU{i}";
                textCPUBlock.Margin = new Thickness(0, 4, 8, 6);
                // 监听 ProgressBar 的值改变事件，更新 TextBlock 的内容
                progressBar.ValueChanged += (sender, e) =>
                {
                    textBlock.Text = $"{progressBar.Value:F0}%";
                };
                textBlock.Margin = new Thickness(0, 4, 8, 6);
                textBlock.HorizontalAlignment = HorizontalAlignment.Right;

                // 设置位置
                Grid.SetRow(textCPUBlock, i);
                Grid.SetColumn(textCPUBlock, 0);

                Grid.SetRow(textBlock, i);
                Grid.SetColumn(textBlock, 1);

                Grid.SetRow(progressBar, i);
                Grid.SetColumn(progressBar, 2);

                // 添加到 Grid 中
                container.Children.Add(textCPUBlock);
                container.Children.Add(progressBar);
                container.Children.Add(textBlock);

                // 将创建的ProgressBar添加到列表中
                progressBars.Add(progressBar);
            }

            return progressBars;
        }

        private void UpdateProgressBars(List<ProgressBar> progressBars, string[] CPUCoreUsageTokens, string CPUCoreNum)
        {
            try
            {
                int numberOfBars = int.Parse(CPUCoreNum);

                for (int i = 0; i < numberOfBars; i++)
                {
                    //throw new Exception($"{progressBars[0].Value}");
                    progressBars[i].Value = double.Parse(CPUCoreUsageTokens[i]);
                }
            }
            catch { }
        }
        private void LoadData()
        {
            try
            {
                if (dataList.CPUCoreTokens != null && dataList.CPUCoreTokens.Length > 0 && !(dataList.CPUCoreTokens.Length == 1 && dataList.CPUCoreTokens[0] == "0"))
                {
                    if (progressBarsGrid.ColumnDefinitions.Count == 0)
                    {
                        progressBars = CreateProgressBars(progressBarsGrid, dataList.CPUCoreTokens, dataList.CPUCoreNum);
                    }
                    else if (progressBars != null)
                    {
                        UpdateProgressBars(progressBars, dataList.CPUCoreTokens, dataList.CPUCoreNum);
                    }
                }
            }
            catch (Exception ex) { Logger.Instance.LogError($"LoadData CPUCoreTokens check failed: {ex.Message}"); }

            try
            {
                MountInfosListView.ItemsSource = dataList.MountInfos;
                NetworkInfosListView.ItemsSource = dataList.NetworkInterfaceInfos;
            }
            catch { }

            try
            {
                if (dataList.SwapUsage != "0%" && dataList.SwapUsage != null)
                {
                    SwapCase1.Visibility = Visibility.Visible;
                    SwapCase2.Visibility = Visibility.Visible;
                    SwapTips1.Visibility = Visibility.Visible;
                    SwapTips2.Visibility = Visibility.Visible;
                }
                else
                {
                    SwapCase1.Visibility = Visibility.Collapsed;
                    SwapCase2.Visibility = Visibility.Collapsed;
                    SwapTips1.Visibility = Visibility.Collapsed;
                    SwapTips2.Visibility = Visibility.Collapsed;

                    dataList.SwapUsage = $"0%";
                    dataList.SwapCached = $"0%";
                    dataList.SwapCachedDisplay = $"0%";
                }
            }
            catch (Exception ex) { }

            // 将数据列表绑定
            dataGrid.DataContext = dataList;
        }
        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            // 获取当前的窗口
            var window = App.MainWindow;
            if (window != null)
            {
                window.Activated += Window_Activated;  // 监听窗口激活事件
            }

            // 创建 DispatcherTimer 并启动
            timer = new DispatcherTimer();
            // 先执行一次事件处理方法
            Timer_Tick(null, null);
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            if (await dataList.UpdateSemaphore.WaitAsync(0))
            {
                // 确保释放信号量
                dataList.UpdateSemaphore.Release();
            }
        }
        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            var window = App.MainWindow;
            if (window != null)
            {
                window.Activated -= Window_Activated;  // 取消事件监听
            }

            // 页面卸载时停止并销毁 DispatcherTimer
            if (timer != null)
            {
                timer.Stop();
                timer.Tick -= Timer_Tick;
                timer = null;
            }
        }

        // 窗口是否活动
        private void Window_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs e)
        {
            int losesFocusStopSSHSelectedIndex = 0;
            if (localSettings.Values["LosesFocusStopSSHSelectedIndex"] != null)
            {
                losesFocusStopSSHSelectedIndex = (int)localSettings.Values["LosesFocusStopSSHSelectedIndex"];
            }

            if (losesFocusStopSSHSelectedIndex == 0)
            {
                if (timer != null)
                {
                    if (e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.Deactivated)
                    {
                        timer.Stop(); // 窗口失去焦点时停止计时器
                    }
                    else if (e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.CodeActivated ||
                             e.WindowActivationState == Microsoft.UI.Xaml.WindowActivationState.PointerActivated)
                    {
                        timer.Start(); // 窗口重新激活时启动计时器
                    }
                }
            }
        }

        // Linux 信息更新
        List<ProgressBar> progressBars = new List<ProgressBar>();
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            var Usages = Method.GetLinuxCPUUsageAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(Usages);

            if (Usages.Result != null)
            {
                Method.UpdateCMSModelFromUsageResult(cmsModel, Usages.Result);
            }
            else
            {
                cmsModel.NumberOfFailuresSec = 60;
                return;
            }

            // DetailPage 特有的 UI 更新（ProgressBars、Swap可见性、ListView绑定）
            if (cmsModel.CPUCoreTokens != null && cmsModel.CPUCoreTokens.Length > 0 && !(cmsModel.CPUCoreTokens.Length == 1 && cmsModel.CPUCoreTokens[0] == "0"))
            {
                if (progressBarsGrid.ColumnDefinitions.Count == 0)
                {
                    progressBars = CreateProgressBars(progressBarsGrid, cmsModel.CPUCoreTokens, cmsModel.CPUCoreNum);
                }
                else if (progressBars != null)
                {
                    UpdateProgressBars(progressBars, cmsModel.CPUCoreTokens, cmsModel.CPUCoreNum);
                }
            }

            // 挂载和网络信息
            if (cmsModel.MountInfos != null)
            {
                MountInfosListView.ItemContainerTransitions = null;
            }
            if (cmsModel.NetworkInterfaceInfos != null)
            {
                NetworkInfosListView.ItemContainerTransitions = null;
            }
            MountInfosListView.ItemsSource = cmsModel.MountInfos;
            NetworkInfosListView.ItemsSource = cmsModel.NetworkInterfaceInfos;

            foreach (MountInfo mountInfo in cmsModel.MountInfos ?? Enumerable.Empty<MountInfo>())
            {
                mountInfo.SectorsReadPerSecond ??= "N/A";
                mountInfo.SectorsWrittenPerSecond ??= "N/A";
                mountInfo.SectorsReadBytes ??= "N/A";
                mountInfo.SectorsWrittenBytes ??= "N/A";
            }

            // Swap 可见性
            if (cmsModel.SwapUsage != "0%" && cmsModel.SwapUsage != null)
            {
                SwapCase1.Visibility = Visibility.Visible;
                SwapCase2.Visibility = Visibility.Visible;
                SwapTips1.Visibility = Visibility.Visible;
                SwapTips2.Visibility = Visibility.Visible;
            }
            else
            {
                SwapCase1.Visibility = Visibility.Collapsed;
                SwapCase2.Visibility = Visibility.Collapsed;
                SwapTips1.Visibility = Visibility.Collapsed;
                SwapTips2.Visibility = Visibility.Collapsed;
            }
        }

        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();
            if (dataList.NumberOfFailuresSec <= 1)
            {
                dataList.NumberOfFailuresStr = $"";
                if (dataList.NumberOfFailures <= 5)
                {
                    // 清空失败计数
                    dataList.NumberOfFailures = 0;

                    // 尝试立即获取信号量，如果无法获取则跳过这次更新
                    if (await dataList.UpdateSemaphore.WaitAsync(0))
                    {
                        Task updateTask = dataList.OSType switch
                        {
                            "Linux" => UpdateLinuxCMSModelAsync(dataList),
                            _ => Task.CompletedTask
                        };

                        tasks.Add(updateTask);
                    }
                    else
                    {
                        // 失败计数
                        dataList.NumberOfFailures += 1;
                    }
                }
                else
                {
                    // 失败倒计时，设置为60
                    dataList.NumberOfFailuresSec = 60;
                    // 清空失败计数
                    dataList.NumberOfFailures = 0;
                }
            }
            else
            {
                dataList.NumberOfFailuresSec -= 1;
                dataList.NumberOfFailuresStr = $"SSH failed ({dataList.NumberOfFailuresSec})";
            }

            await Task.WhenAll(tasks);
        }
        private void OpenSSHTerminal_Click(object sender, RoutedEventArgs e)
        {
            App.m_window.NavigateToPage(typeof(TerminalPage), dataList);
        }
        private void OpenTopPage_Click(object sender, RoutedEventArgs e)
        {
            App.m_window.NavigateToPage(typeof(TopPage), dataList);
        }
        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            EditThisConfig(dataList);
        }

        private async void ReloadPage_Click(object sender, RoutedEventArgs e)
        {
            dataList.NumberOfFailuresStr = null;
            dataList.NumberOfFailures = 0;
            dataList.NumberOfFailuresSec = 0;
            App.m_window.NavigateToPage(typeof(DetailPage), dataList);
        }
        private async void EditThisConfig(CMSModel cmsModel)
        {
            ContentDialogResult result = await ShowAddServerDialogAsync(cmsModel, resourceLoader.GetString("DialogChange"));

            // 如果按下了Primary
            if (result == ContentDialogResult.Primary)
            {
                // 实例化SQLiteHelper
                SQLiteHelper dbHelper = new SQLiteHelper();
                // 更新数据
                dbHelper.UpdateData(cmsModel);
                // 重新加载数据
                LoadData();
                // 去掉绑定
                MountInfosListView.ItemsSource = null;
                NetworkInfosListView.ItemsSource = null;
                Logger.Instance.LogInfo("Edit Config is completed.");
            }
        }

        private async Task<ContentDialogResult> ShowAddServerDialogAsync(CMSModel cmsModel, string primaryButtonText)
        {
            string pendingPlainPassword = null;

            while (true)
            {
                AddServer dialog = new AddServer(cmsModel, pendingPlainPassword);
                dialog.XamlRoot = this.XamlRoot;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                dialog.PrimaryButtonText = primaryButtonText;
                dialog.CloseButtonText = resourceLoader.GetString("DialogClose");
                dialog.DefaultButton = ContentDialogButton.Primary;

                ContentDialogResult result = await dialog.ShowAsync();
                if (!dialog.ManageSSHKeysRequested)
                {
                    return result;
                }

                pendingPlainPassword = dialog.PendingPlainPassword;
                await ShowManageSSHKeysDialogAsync();
            }
        }

        private async Task ShowManageSSHKeysDialogAsync()
        {
            ManageSSHKeys keyDialog = new ManageSSHKeys();
            keyDialog.XamlRoot = this.XamlRoot;
            keyDialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
            keyDialog.CloseButtonText = resourceLoader.GetString("Cancel");
            await keyDialog.ShowAsync();
        }
    }
}
