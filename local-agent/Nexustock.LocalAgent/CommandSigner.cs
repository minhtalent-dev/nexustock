using System;
using System.Security.Cryptography;
using System.Text;

namespace Nexustock.LocalAgent;

public static class CommandSigner
{
    public static bool VerifySignature(string payloadJson, string signature, string agentToken)
    {
        try
        {
            // TokenHash là key dùng để ký từ Backend
            using var sha256 = SHA256.Create();
            var tokenBytes = Encoding.UTF8.GetBytes(agentToken);
            var tokenHashBytes = sha256.ComputeHash(tokenBytes);

            using var hmac = new HMACSHA256(tokenHashBytes);
            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
            var computedHash = hmac.ComputeHash(payloadBytes);
            var computedSignature = Convert.ToBase64String(computedHash);

            return FixedTimeEquals(signature, computedSignature);
        }
        catch
        {
            return false;
        }
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        if (left == null || right == null) return false;
        if (left.Length != right.Length) return false;
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
