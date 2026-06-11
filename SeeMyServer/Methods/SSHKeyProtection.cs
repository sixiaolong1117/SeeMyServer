using System;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;
using Windows.Storage.Streams;

namespace SeeMyServer.Methods
{
    public static class SSHKeyProtection
    {
        private const string ProtectionDescriptor = "LOCAL=user AND LOCAL=MACHINE";
        private const string LegacyProtectionDescriptor = "LOCAL=user";

        public static string Protect(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return "";
            }

            byte[] plainBytes = null;
            byte[] protectedBytes = null;
            try
            {
                plainBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
                DataProtectionProvider provider = new DataProtectionProvider(ProtectionDescriptor);
                IBuffer plainBuffer = CryptographicBuffer.CreateFromByteArray(plainBytes);
                IBuffer protectedBuffer = provider.ProtectAsync(plainBuffer).AsTask().GetAwaiter().GetResult();
                CryptographicBuffer.CopyToByteArray(protectedBuffer, out protectedBytes);
                return Convert.ToBase64String(protectedBytes);
            }
            finally
            {
                if (plainBytes != null) Array.Clear(plainBytes, 0, plainBytes.Length);
                if (protectedBytes != null) Array.Clear(protectedBytes, 0, protectedBytes.Length);
            }
        }

        public static string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return "";
            }

            // 先尝试新描述符（LOCAL=user AND LOCAL=MACHINE）
            string result = TryUnprotect(protectedText, ProtectionDescriptor);
            if (result != null)
            {
                return result;
            }

            // 回退到旧描述符（LOCAL=user），兼容旧版数据
            result = TryUnprotect(protectedText, LegacyProtectionDescriptor);
            if (result != null)
            {
                return result;
            }

            throw new InvalidOperationException("无法解密 SSH 密钥：DPAPI 描述符不匹配。");
        }

        private static string TryUnprotect(string protectedText, string descriptor)
        {
            byte[] protectedBytes = null;
            byte[] plainBytes = null;
            try
            {
                protectedBytes = Convert.FromBase64String(protectedText);
                IBuffer protectedBuffer = CryptographicBuffer.CreateFromByteArray(protectedBytes);
                DataProtectionProvider provider = new DataProtectionProvider(descriptor);
                IBuffer plainBuffer = provider.UnprotectAsync(protectedBuffer).AsTask().GetAwaiter().GetResult();
                CryptographicBuffer.CopyToByteArray(plainBuffer, out plainBytes);
                return System.Text.Encoding.UTF8.GetString(plainBytes);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (protectedBytes != null) Array.Clear(protectedBytes, 0, protectedBytes.Length);
                if (plainBytes != null) Array.Clear(plainBytes, 0, plainBytes.Length);
            }
        }

        public static bool IsLegacyEncrypted(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                return false;
            }

            // 如果新描述符能解密，就不是 legacy
            string result = TryUnprotect(protectedText, ProtectionDescriptor);
            if (result != null)
            {
                return false;
            }

            // 如果旧描述符能解密，就是 legacy
            result = TryUnprotect(protectedText, LegacyProtectionDescriptor);
            return result != null;
        }

        public static string ReProtect(string legacyEncryptedText)
        {
            string plainText = Unprotect(legacyEncryptedText);
            try
            {
                return Protect(plainText);
            }
            finally
            {
                plainText = null;
            }
        }
    }
}
