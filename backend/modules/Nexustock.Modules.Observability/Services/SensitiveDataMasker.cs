using System;
using System.Text.RegularExpressions;

namespace Nexustock.Modules.Observability.Services;

/// <summary>
/// Hỗ trợ mask dữ liệu nhạy cảm trước khi lưu vết.
/// </summary>
public static class SensitiveDataMasker
{
    private static readonly string[] SensitiveKeys = new[] 
    { 
        "password", 
        "token", 
        "secret", 
        "connectionstring", 
        "secretkey",
        "authorization",
        "jwt"
    };

    public static string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var result = input;
        foreach (var key in SensitiveKeys)
        {
            // JSON format: "password": "xyz"
            var jsonPattern = $@"""({key})""\s*:\s*""[^""]*""";
            result = Regex.Replace(result, jsonPattern, m =>
            {
                var keyName = Regex.Match(m.Value, $@"""({key})""", RegexOptions.IgnoreCase).Groups[1].Value;
                return $@"""{keyName}"": ""***""";
            }, RegexOptions.IgnoreCase);

            // Key-Value format: password=xyz
            var kvPattern = $@"\b({key})\s*=\s*[^;\s&]+";
            result = Regex.Replace(result, kvPattern, m =>
            {
                var keyName = Regex.Match(m.Value, $@"\b({key})", RegexOptions.IgnoreCase).Groups[1].Value;
                return $"{keyName}=***";
            }, RegexOptions.IgnoreCase);
        }
        return result;
    }
}
