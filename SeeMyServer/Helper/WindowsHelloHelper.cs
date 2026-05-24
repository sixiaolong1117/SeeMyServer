using System;
using System.Threading.Tasks;
using Windows.Security.Credentials.UI;
using Windows.Storage;

namespace SeeMyServer.Helper
{
    /// <summary>
    /// Windows Hello 安全鉴权助手
    /// 用于保护 SSH 密钥管理操作
    /// </summary>
    public static class WindowsHelloHelper
    {
        private const string SettingKey = "WindowsHelloEnabled";

        /// <summary>
        /// 是否启用 Windows Hello 保护（默认关闭）
        /// </summary>
        public static bool IsEnabled
        {
            get
            {
                object value = ApplicationData.Current.LocalSettings.Values[SettingKey];
                return value is bool b && b;
            }
            private set
            {
                ApplicationData.Current.LocalSettings.Values[SettingKey] = value;
            }
        }

        /// <summary>
        /// 检查当前设备是否支持 Windows Hello
        /// </summary>
        public static async Task<bool> IsAvailableAsync()
        {
            UserConsentVerifierAvailability availability = await UserConsentVerifier.CheckAvailabilityAsync().AsTask();
            return availability == UserConsentVerifierAvailability.Available;
        }

        /// <summary>
        /// 如果启用了 Windows Hello 保护，则要求用户验证身份
        /// </summary>
        /// <param name="message">验证提示信息</param>
        /// <returns>true=验证通过或未启用保护，false=验证失败</returns>
        public static async Task<bool> VerifyAsync(string message)
        {
            if (!IsEnabled)
            {
                return true; // 未启用保护，直接放行
            }

            UserConsentVerificationResult result = await UserConsentVerifier.RequestVerificationAsync(message).AsTask();
            return result == UserConsentVerificationResult.Verified;
        }

        /// <summary>
        /// 启用 Windows Hello 保护（需要先验证身份）
        /// </summary>
        /// <returns>true=启用成功，false=验证失败</returns>
        public static async Task<bool> EnableAsync(string message)
        {
            UserConsentVerificationResult result = await UserConsentVerifier.RequestVerificationAsync(message).AsTask();
            if (result == UserConsentVerificationResult.Verified)
            {
                IsEnabled = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 关闭 Windows Hello 保护（需要先验证身份）
        /// </summary>
        /// <returns>true=关闭成功，false=验证失败</returns>
        public static async Task<bool> DisableAsync(string message)
        {
            UserConsentVerificationResult result = await UserConsentVerifier.RequestVerificationAsync(message).AsTask();
            if (result == UserConsentVerificationResult.Verified)
            {
                IsEnabled = false;
                return true;
            }
            return false;
        }
    }
}
