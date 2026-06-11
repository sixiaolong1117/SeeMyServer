using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using SeeMyServer.Datas;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using SeeMyServer.Models;
using SeeMyServer.Pages.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using CommunityToolkit.WinUI.Controls;
using static PInvoke.User32;
using PInvoke;
using System.Collections.ObjectModel;
using System.Linq;
using System.Collections.Specialized;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.ApplicationModel.Core;

namespace SeeMyServer.Pages
{
    public sealed partial class HomePage : Page
    {
        // 启用本地设置数据
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ResourceLoader resourceLoader = new ResourceLoader();
        private DispatcherQueue _dispatcherQueue;
        private DispatcherTimer timer;
        private Logger logger;

        public HomePage()
        {
            this.InitializeComponent();

            this.Loaded += Page_Loaded;
            this.Unloaded += Page_Unloaded;

            // 设置日志，最大1MB
            logger = new Logger(1);

            // 获取UI线程的DispatcherQueue
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            // 页面初始化后，加载数据
            LoadString();
            LoadData();
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            dataListView.SelectedItem = null;
        }
        private void LoadString()
        {
            ConfirmDelete.Content = resourceLoader.GetString("Confirm");
            CancelDelete.Content = resourceLoader.GetString("Cancel");
        }

        private ObservableCollection<CMSModel> dataList;

        private void LoadData()
        {
            // 加载数据
            List<CMSModel> loadedData = LoadDataFromDatabase();

            // 解析排序序列，并合并数据库中尚未写入排序设置的新配置。
            List<int> sortOrder = LoadSortOrder();
            if (sortOrder.Count == 0)
            {
                sortOrder = loadedData.Select(item => item.Id).ToList();
            }
            else
            {
                HashSet<int> existingIds = loadedData.Select(item => item.Id).ToHashSet();
                sortOrder = sortOrder.Where(existingIds.Contains).ToList();

                HashSet<int> sortedIds = sortOrder.ToHashSet();
                foreach (CMSModel item in loadedData)
                {
                    if (sortedIds.Add(item.Id))
                    {
                        sortOrder.Add(item.Id);
                    }
                }
            }

            SaveSortOrder(sortOrder);

            // 根据排序序列对 dataList 进行排序
            dataList = new ObservableCollection<CMSModel>(sortOrder
                                            .Select(id => loadedData.FirstOrDefault(item => item.Id == id))
                                            .Where(item => item != null));

            // 添加事件处理程序
            dataList.CollectionChanged += DataList_CollectionChanged;

            // 设置数据源
            dataListView.ItemsSource = dataList;

            string idsString = string.Join(", ", dataList.Select(item => item.Id));

            // 初始化占用
            foreach (CMSModel cmsModel in dataList)
            {
                InitItemDisplay(cmsModel);
            }
        }

        private List<int> LoadSortOrder()
        {
            string sortOrderString = localSettings.Values["DataListOrder"] as string;
            if (string.IsNullOrWhiteSpace(sortOrderString))
            {
                return new List<int>();
            }

            return sortOrderString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                  .Select(str => int.TryParse(str.Trim(), out int id) ? id : 0)
                                  .Where(id => id > 0)
                                  .Distinct()
                                  .ToList();
        }

        private void SaveSortOrder(IEnumerable<int> sortOrder)
        {
            localSettings.Values["DataListOrder"] = string.Join(",", sortOrder);
        }

        private void DataList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            string idsString = string.Join(",", dataList.Select(item => item.Id));
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    logger.LogInfo("Items added:");
                    foreach (var item in e.NewItems)
                    {
                        logger.LogInfo($"Id: {(item as CMSModel).Id}, Name: {(item as CMSModel).Name}");
                    }
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    logger.LogInfo("Items removed:");
                    foreach (var item in e.OldItems)
                    {
                        logger.LogInfo($"Id: {(item as CMSModel).Id}, Name: {(item as CMSModel).Name}");
                    }
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    logger.LogInfo("Items replaced:");
                    foreach (var newItem in e.NewItems)
                    {
                        logger.LogInfo($"New Id: {(newItem as CMSModel).Id}, New Name: {(newItem as CMSModel).Name}");
                    }
                    foreach (var oldItem in e.OldItems)
                    {
                        logger.LogInfo($"Old Id: {(oldItem as CMSModel).Id}, Old Name: {(oldItem as CMSModel).Name}");
                    }
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    logger.LogInfo("Collection reset.");
                    logger.LogInfo(idsString);
                    break;
                case NotifyCollectionChangedAction.Move:
                    logger.LogInfo($"Item moved from index {e.OldStartingIndex} to index {e.NewStartingIndex}.");
                    logger.LogInfo(idsString);
                    break;
                default:
                    break;
            }
            SaveSortOrder(dataList.Select(item => item.Id));
        }

        private List<CMSModel> LoadDataFromDatabase()
        {
            // 实例化 SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();

            // 查询数据
            return dbHelper.QueryData();
        }
        // 初始化显示
        private void InitItemDisplay(CMSModel cmsModel)
        {
            cmsModel.CPUUsage = "0%";
            cmsModel.MEMUsage = "0%";
            cmsModel.NETSent = "0 B/s ↑";
            cmsModel.NETReceived = "0 B/s ↓";
            cmsModel.DISKRead = "0 B/s R";
            cmsModel.DISKWrite = "0 B/s W";
        }
        // 在某处添加新项
        private void AddItem(CMSModel cmsModel)
        {
            dataList.Add(cmsModel);
            InitItemDisplay(cmsModel);

            // 手动通知 dataListView 更新
            RefreshListView();
        }

        // 在某处移除项
        private void RemoveItem(CMSModel cmsModel)
        {
            dataList.Remove(cmsModel);
            InitItemDisplay(cmsModel);

            // 手动通知 dataListView 更新
            RefreshListView();
        }

        // 手动更新 dataListView
        private void RefreshListView()
        {
            dataListView.ItemsSource = null;
            dataListView.ItemsSource = dataList;
            foreach (CMSModel cmsModel in dataList)
            {
                cmsModel.NumberOfFailures = 0;
                cmsModel.NumberOfFailuresStr = null;
                cmsModel.NumberOfFailuresSec = 0;
            }
        }

        // 加载页面
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

            foreach (CMSModel cmsModel in dataList)
            {
                if (await cmsModel.UpdateSemaphore.WaitAsync(0))
                {
                    // 确保释放信号量
                    cmsModel.UpdateSemaphore.Release();
                }
            }
        }

        // 卸载页面
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
        private async Task UpdateLinuxCMSModelAsync(CMSModel cmsModel)
        {
            // 定义异步任务
            var Usages = Method.GetLinuxCPUUsageAsync(cmsModel);

            // 同时执行异步任务
            await Task.WhenAll(Usages);

            if (Usages.Result != null)
            {
                cmsModel.NumberOfFailures = 0;
                Method.UpdateCMSModelFromUsageResult(cmsModel, Usages.Result, logger);
            }
        }
        private async void Timer_Tick(object sender, object e)
        {
            List<Task> tasks = new List<Task>();

            foreach (CMSModel cmsModel in dataList)
            {
                if (cmsModel.NumberOfFailuresSec <= 0)
                {
                    cmsModel.NumberOfFailuresStr = $"";
                    if (cmsModel.NumberOfFailures <= 5)
                    {
                        // 尝试立即获取信号量，如果无法获取则跳过这次更新
                        if (await cmsModel.UpdateSemaphore.WaitAsync(0))
                        {
                            // 清空失败计数
                            cmsModel.NumberOfFailures = 0;

                            Task updateTask = cmsModel.OSType switch
                            {
                                "Linux" => UpdateLinuxCMSModelAsync(cmsModel),
                                _ => Task.CompletedTask
                            };

                            tasks.Add(updateTask);
                        }
                        else
                        {
                            // 失败计数
                            cmsModel.NumberOfFailures += 1;
                        }
                    }
                    else
                    {
                        // 失败倒计时，设置为60
                        cmsModel.NumberOfFailuresSec = 60;
                        // 清空失败计数
                        cmsModel.NumberOfFailures = 0;
                    }
                }
                else
                {
                    cmsModel.NumberOfFailuresSec -= 1;
                    cmsModel.NumberOfFailuresStr = $"SSH failed ({cmsModel.NumberOfFailuresSec})";
                }
            }

            await Task.WhenAll(tasks);
        }
        // 添加/修改配置按钮点击
        private async void AddConfigButton_Click(object sender, RoutedEventArgs e)
        {
            // 创建一个初始的CMSModel对象
            CMSModel initialCMSModelData = new CMSModel();
            // 初始化该CMSModel对象
            initialCMSModelData.HostPort = "22";
            initialCMSModelData.OSType = "Linux";

            ContentDialogResult result = await ShowAddServerDialogAsync(initialCMSModelData, resourceLoader.GetString("DialogAdd"));

            // 如果按下了Primary
            if (result == ContentDialogResult.Primary)
            {
                // 实例化SQLiteHelper
                SQLiteHelper dbHelper = new SQLiteHelper();
                // 插入新数据，并将Id返回。
                int id = dbHelper.InsertData(initialCMSModelData);
                // 加载数据
                initialCMSModelData.Id = id;
                AddItem(initialCMSModelData);
                logger.LogInfo("Add Config is completed.");
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

        // 导入配置按钮点击
        private async void ImportConfig_Click(object sender, RoutedEventArgs e)
        {
            HomePageImportConfig.IsEnabled = false;
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();
            // 获取导入的数据
            CMSModel cmsModel = await Method.ImportConfig();
            if (cmsModel != null)
            {
                // 插入新数据
                int id = dbHelper.InsertData(cmsModel);
                // 重新加载数据
                //LoadData();
                cmsModel.Id = id;
                AddItem(cmsModel);
                logger.LogInfo("Import Config is completed.");
            }
            HomePageImportConfig.IsEnabled = true;
        }
        private async void ReloadPage_Click(object sender, RoutedEventArgs e)
        {
            //App.m_window.NavigateToPage(typeof(HomePage));
            RefreshListView();
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
                //LoadData();
                RefreshListView();
                logger.LogInfo("Edit Config is completed.");
            }
        }
        private void ConfirmDelete_Click(object sender, RoutedEventArgs e)
        {
            // 关闭二次确认Flyout
            confirmationDelFlyout.Hide();
            // 获取NSModel对象
            CMSModel selectedModel = (CMSModel)dataListView.SelectedItem;
            // 实例化SQLiteHelper
            SQLiteHelper dbHelper = new SQLiteHelper();
            // 删除数据
            dbHelper.DeleteData(selectedModel);
            // 重新加载数据
            //LoadData();
            RemoveItem(selectedModel);
            logger.LogInfo("Delete Config is completed.");
        }
        private void CancelDelete_Click(object sender, RoutedEventArgs e)
        {
            // 关闭二次确认Flyout
            confirmationDelFlyout.Hide();
        }
        private async void ExportConfigFunction(CMSModel cmsModel)
        {
            string result = await Method.ExportConfig(cmsModel);
        }
        private void OnListViewDoubleTapped(object sender, RoutedEventArgs e)
        { }
        private void OnListViewRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // 获取右键点击的ListViewItem
            FrameworkElement listViewItem = (sender as FrameworkElement);

            // 获取右键点击的数据对象（NSModel）
            CMSModel selectedItem = listViewItem?.DataContext as CMSModel;

            if (selectedItem != null)
            {
                // 将右键点击的项设置为选中项
                dataListView.SelectedItem = selectedItem;
                // 创建ContextMenu
                MenuFlyout menuFlyout = new MenuFlyout();

                // 打开终端
                MenuFlyoutItem terminalMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("terminalMenuItemText")
                };
                terminalMenuItem.Click += async (sender, e) =>
                {
                    var dialog = new TerminalDialog(selectedItem);
                    dialog.XamlRoot = this.XamlRoot;
                    await dialog.ShowAsync();
                };
                menuFlyout.Items.Add(terminalMenuItem);

                // 添加分割线
                MenuFlyoutSeparator separator = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator);

                // 编辑
                MenuFlyoutItem editMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("editMenuItemText")
                };
                editMenuItem.Click += (sender, e) =>
                {
                    EditThisConfig(selectedItem);
                };
                menuFlyout.Items.Add(editMenuItem);

                // 删除
                MenuFlyoutItem deleteMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("deleteMenuItemText")
                };
                deleteMenuItem.Click += (sender, e) =>
                {
                    // 弹出二次确认Flyout
                    confirmationDelFlyout.ShowAt(listViewItem);
                };
                menuFlyout.Items.Add(deleteMenuItem);

                // 添加分割线
                MenuFlyoutSeparator separator2 = new MenuFlyoutSeparator();
                menuFlyout.Items.Add(separator2);

                // 导出
                MenuFlyoutItem exportMenuItem = new MenuFlyoutItem
                {
                    Text = resourceLoader.GetString("exportMenuItemText")
                };
                exportMenuItem.Click += (sender, e) =>
                {
                    ExportConfigFunction(selectedItem);
                };
                menuFlyout.Items.Add(exportMenuItem);

                // 在指定位置显示ContextMenu
                menuFlyout.ShowAt(listViewItem, e.GetPosition(listViewItem));
            }
        }
        // 处理单击事件的代码
        private void ContentGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem != null)
            {
                // 获取点击的数据对象
                CMSModel selectedItem = e.ClickedItem as CMSModel;

                // 导航到页面
                App.m_window.NavigateToPage(typeof(DetailPage), selectedItem);
            }
        }
    }
}
