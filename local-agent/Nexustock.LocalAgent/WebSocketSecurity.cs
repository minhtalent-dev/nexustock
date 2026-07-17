using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Nexustock.LocalAgent;

public static class WebSocketSecurity
{
    public static bool MatchOrigin(string origin, string allowedPattern)
    {
        if (string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(allowedPattern)) return false;
        if (allowedPattern == "*") return true;

        if (!allowedPattern.Contains('*'))
        {
            return string.Equals(origin, allowedPattern, StringComparison.OrdinalIgnoreCase);
        }

        var regexPattern = "^" + Regex.Escape(allowedPattern).Replace(@"\*", ".*") + "$";
        return Regex.IsMatch(origin, regexPattern, RegexOptions.IgnoreCase);
    }

    public static bool HasValidTimestamp(string timestamp, out string errorCode, out string errorMessage)
    {
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (!DateTime.TryParse(timestamp, out var messageTime))
        {
            errorCode = "agent.invalid_format";
            errorMessage = "Định dạng timestamp không hợp lệ (yêu cầu ISO 8601).";
            return false;
        }

        if (Math.Abs((DateTime.UtcNow - messageTime.ToUniversalTime()).TotalSeconds) > 30)
        {
            errorCode = "auth.time_skew";
            errorMessage = "Sai lệch thời gian quá 30 giây. Vui lòng đồng bộ giờ hệ thống.";
            return false;
        }

        return true;
    }

    public static bool VerifySignedPayload(WebSocketMessage msg, AgentConfig config, out string errorCode, out string errorMessage)
    {
        errorCode = string.Empty;
        errorMessage = string.Empty;

        if (IsTestSignatureBypassAllowed(msg, config))
        {
            return true;
        }

        if (string.IsNullOrEmpty(msg.Signature))
        {
            errorCode = "auth.signature_missing";
            errorMessage = "Thiếu chữ ký xác thực signature.";
            return false;
        }

        if (!Enum.TryParse<DataProtectionScope>(config.DpapiScope, out var scope))
        {
            errorCode = "agent.dpapi_failed";
            errorMessage = "Cấu hình DPAPI scope không hợp lệ.";
            return false;
        }

        var token = DpapiSecretStorage.Decrypt(config.EncryptedAgentToken!, scope);
        if (string.IsNullOrEmpty(token))
        {
            errorCode = "agent.dpapi_failed";
            errorMessage = "Lỗi giải mã token bảo mật cục bộ.";
            return false;
        }

        var payloadJson = msg.Payload.GetRawText();
        if (!CommandSigner.VerifySignature(payloadJson, msg.Signature, token))
        {
            errorCode = "auth.invalid_signature";
            errorMessage = "Chữ ký tin nhắn không hợp lệ.";
            return false;
        }

        return true;
    }

    private static bool IsTestSignatureBypassAllowed(WebSocketMessage msg, AgentConfig config)
    {
        if (!config.AllowTestSignatureBypass) return false;
        if (!string.Equals(Environment.GetEnvironmentVariable("NEXUSTOCK_AGENT_TEST_MODE"), "true", StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.Equals(msg.Signature, "NEXUSTOCK_TEST_SIGNATURE", StringComparison.Ordinal)) return false;

        return msg.Type == "printer.print.request";
    }
}
