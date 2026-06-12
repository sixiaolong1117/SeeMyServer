using Microsoft.UI.Xaml;
using SeeMyServer.Helper;
using SeeMyServer.Methods;
using System;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.ApplicationModel.Resources;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;

namespace SeeMyServer
{
    public partial class App : Application
    {
        // 静态属性，保存 MainWindow 实例，提供全局访问
        public static MainWindow MainWindow => m_window;

        // 私有的 m_window，用于引用 MainWindow 实例
        public static MainWindow m_window;

        // 日志记录器
        private Logger logger;

        public App()
        {
            this.InitializeComponent();

            // 设置日志，最大1MB
            logger = new Logger(1);
        }

        protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            // 获取当前应用实例
            var currentInstance = AppInstance.GetCurrent();

            // 注册或查找已经运行的实例
            var instance = AppInstance.FindOrRegisterForKey("SeeMyServerAppInstance-NBL&iUpczgYqQ27EoDB#JbFm*hh1yfoC");

            // 如果已经有实例在运行，转移激活到已有实例
            if (!instance.IsCurrent)
            {
                // 将激活转移到已有实例
                await instance.RedirectActivationToAsync(currentInstance.GetActivatedEventArgs());

                // 从 resources.resw 中获取窗口标题
                var resourceLoader = new ResourceLoader();
                string appTitle = resourceLoader.GetString("AppTitle"); // 动态加载 AppTitle 资源

                // 确保已激活的实例获得焦点并显示在前台
                HWND existingHwnd = FindWindow(null, appTitle);
                if (!existingHwnd.IsNull)
                {
                    // 显示窗口并设置为前台
                    ShowWindow(existingHwnd, SHOW_WINDOW_CMD.SW_RESTORE); // 如果窗口最小化，则恢复
                    SetForegroundWindow(existingHwnd); // 将窗口设为前台
                }

                // 退出当前实例
                Environment.Exit(0);
            }

            // 创建 MainWindow 实例并赋值给 m_window
            m_window = new MainWindow();

            // 获取窗口句柄
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(m_window);

            // 设置窗口大小（如果需要，取消注释）
            // SetWindowSize(hwnd, 1110, 800);

            // 记录启动日志
            logger.LogInfo("ServerDash starts.");

            // 清理遗留的临时私钥文件
            Method.CleanupTempSSHKeys();

            // 激活窗口
            m_window.Activate();
        }

        // 调整窗口大小方法
        private void SetWindowSize(IntPtr hwnd, int width, int height)
        {
            // 获取 DPI 缩放比例
            var dpi = GetDpiForWindow((HWND)hwnd);
            float scalingFactor = (float)dpi / 96;

            // 根据缩放比例调整窗口尺寸
            width = (int)(width * scalingFactor);
            height = (int)(height * scalingFactor);

            // 设置窗口大小，不移动窗口位置
            SetWindowPos(
                (HWND)hwnd,
                (HWND)IntPtr.Zero,
                0, 0, width, height,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE
            );
        }
    }

}
