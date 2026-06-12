using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Runtime.InteropServices;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using WinRT;
using System;
using Microsoft.UI.Xaml.Navigation;
using SeeMyServer.Pages;
using System.Security.Cryptography;
using SeeMyServer.Methods;
using SeeMyServer.Models;

namespace SeeMyServer
{
    public sealed partial class MainWindow : Window
    {
        WindowsSystemDispatcherQueueHelper m_wsdqHelper; // See below for implementation.
        DesktopAcrylicController a_backdropController;
        MicaController m_backdropController;
        MicaController ma_backdropController;
        SystemBackdropConfiguration m_configurationSource;
        ResourceLoader resourceLoader = new ResourceLoader();
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        public MainWindow()
        {
            this.InitializeComponent();

            // 启动个性化TitleBar
            ExtendsContentIntoTitleBar = true;
            // 将UI设置为TitleBar
            SetTitleBar(AppTitleBar);
            // 设置任务栏显示名称
            Title = resourceLoader.GetString("AppTitle");

            TrySetSystemBackdrop();

            NavView.SelectedItem = NavView.MenuItems[0];

            AppTitleTextBlock.Text = resourceLoader.GetString("AppTitle");

            contentFrame.Navigated += ContentFrame_Navigated;
        }
        bool TrySetSystemBackdrop()
        {
            // 检查是否支持 Mica 控制器
            if (MicaController.IsSupported())
            {
                // 创建 Windows 系统分发队列助手
                m_wsdqHelper = new WindowsSystemDispatcherQueueHelper();
                m_wsdqHelper.EnsureWindowsSystemDispatcherQueueController();

                // 创建配置对象
                m_configurationSource = new SystemBackdropConfiguration();
                // 窗口激活时的事件处理
                this.Activated += Window_Activated;
                // 窗口关闭时的事件处理
                this.Closed += Window_Closed;
                // 窗口主题更改时的事件处理
                ((FrameworkElement)this.Content).ActualThemeChanged += Window_ThemeChanged;

                // 初始配置状态
                m_configurationSource.IsInputActive = true;
                // 设置配置对象的主题
                SetConfigurationSourceTheme();

                // 根据本地设置创建相应的背景控制器
                if (localSettings.Values["materialStatus"] as string == "Acrylic")
                {
                    a_backdropController = new DesktopAcrylicController();
                }
                else if (localSettings.Values["materialStatus"] as string == "Mica")
                {
                    m_backdropController = new MicaController();
                }
                else
                {
                    ma_backdropController = new MicaController()
                    {
                        Kind = MicaKind.BaseAlt
                    };
                }

                // 启用系统背景
                // 注意：确保使用 "using WinRT;" 以支持 Window.As<...>() 调用
                if (localSettings.Values["materialStatus"] as string == "Mica")
                {
                    m_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    m_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                else if (localSettings.Values["materialStatus"] as string == "Acrylic")
                {
                    a_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    a_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                else
                {
                    localSettings.Values["materialStatus"] = "Mica Alt";
                    ma_backdropController.AddSystemBackdropTarget(this.As<Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop>());
                    ma_backdropController.SetSystemBackdropConfiguration(m_configurationSource);
                }
                return true;
            }

            return false;
        }
        private void Window_Activated(object sender, WindowActivatedEventArgs args)
        {
            // 设置配置源的输入活动状态，如果窗口激活状态不是被停用，则为true
            m_configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // 确保任何 Mica/Acrylic 控制器都被释放
            // 这样它就不会尝试使用已关闭的窗口。
            if (localSettings.Values["materialStatus"] as string == "Acrylic")
            {
                if (a_backdropController != null)
                {
                    a_backdropController.Dispose();
                    a_backdropController = null;
                }
            }
            else if (localSettings.Values["materialStatus"] as string == "Mica")
            {
                if (m_backdropController != null)
                {
                    m_backdropController.Dispose();
                    m_backdropController = null;
                }
            }
            else
            {
                if (ma_backdropController != null)
                {
                    ma_backdropController.Dispose();
                    ma_backdropController = null;
                }
            }
            // 移除窗口激活事件处理程序
            this.Activated -= Window_Activated;
            // 清空配置源
            m_configurationSource = null;
        }

        private void Window_ThemeChanged(FrameworkElement sender, object args)
        {
            if (m_configurationSource != null)
            {
                SetConfigurationSourceTheme();
            }
        }

        private void SetConfigurationSourceTheme()
        {
            switch (((FrameworkElement)this.Content).ActualTheme)
            {
                case ElementTheme.Dark: m_configurationSource.Theme = SystemBackdropTheme.Dark; break;
                case ElementTheme.Light: m_configurationSource.Theme = SystemBackdropTheme.Light; break;
                case ElementTheme.Default: m_configurationSource.Theme = SystemBackdropTheme.Default; break;
            }
        }

        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                contentFrame.Navigate(typeof(SettingsPage));
            }
            else
            {
                var selectedItem = args.SelectedItem as NavigationViewItem;
                if (selectedItem != null)
                {
                    switch (selectedItem.Tag.ToString())
                    {
                        case "HomePage":
                            contentFrame.Navigate(typeof(HomePage));
                            break;
                        case "About":
                            contentFrame.Navigate(typeof(About));
                            break;
                    }
                }
            }
        }
        public void NavigateToPage(Type pageType, CMSModel cmsModel)
        {
            contentFrame.Navigate(pageType, cmsModel);
            // 更新选中的导航项
            var selectedItem = FindSelectedItemByTag(contentFrame.CurrentSourcePageType.Name);
            if (selectedItem != null)
            {
                NavView.SelectedItem = selectedItem;
            }
            else
            {
                NavView.SelectedItem = null;
            }
        }
        private void BackButton_Click(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if (contentFrame.CanGoBack)
            {
                contentFrame.GoBack();
            }
            else
            {
                contentFrame.Navigate(typeof(HomePage));
            }
        }
        private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
        {
            var selectedItem = FindSelectedItemByTag(contentFrame.CurrentSourcePageType.Name);
            if (selectedItem != null)
            {
                NavView.SelectedItem = selectedItem;
            }
            else
            {
                NavView.SelectedItem = null;
            }
        }
        private NavigationViewItem FindSelectedItemByTag(string tag)
        {
            foreach (NavigationViewItem item in NavView.MenuItems)
            {
                if (item.Tag.ToString() == tag)
                {
                    return item;
                }
            }

            foreach (NavigationViewItem item in NavView.FooterMenuItems)
            {
                if (item.Tag.ToString() == tag)
                {
                    return item;
                }
            }

            return null;
        }
    }

    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
    }
}
