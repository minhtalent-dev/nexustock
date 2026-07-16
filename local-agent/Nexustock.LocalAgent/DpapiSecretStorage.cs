using System;
using System.Security.Cryptography;
using System.Text;

namespace Nexustock.LocalAgent;

public static class DpapiSecretStorage
{
    private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes("NexustockAgentEntropy2026");

    public static string Encrypt(string plainText, DataProtectionScope scope)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = ProtectedData.Protect(plainBytes, OptionalEntropy, scope);
        return Convert.ToBase64String(encryptedBytes);
    }

    public static string Decrypt(string encryptedBase64, DataProtectionScope scope)
    {
        if (string.IsNullOrEmpty(encryptedBase64)) return string.Empty;
        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedBase64);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, OptionalEntropy, scope);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }
}
