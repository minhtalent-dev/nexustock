using System;
using System.Security.Cryptography;
using System.Text;

namespace Nexustock.Modules.Webhook.Services;

public interface IWebhookSigningService
{
    string ComputeSignature(string secretKey, string timestamp, string payload);
}

public class WebhookSigningService : IWebhookSigningService
{
    /// <summary>
    /// Tính HMAC-SHA256 signature: HMAC(secretKey, "{timestamp}.{payload}")
    /// </summary>
    public string ComputeSignature(string secretKey, string timestamp, string payload)
    {
        var message = $"{timestamp}.{payload}";
        var keyBytes = Encoding.UTF8.GetBytes(secretKey);
        var msgBytes = Encoding.UTF8.GetBytes(message);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(msgBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
