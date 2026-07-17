using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Nexustock.Modules.LabelPrinting.Services;

public class LabelTemplateRenderer
{
    private static readonly Regex TokenRegex = new(@"\{\{([a-zA-Z][a-zA-Z0-9_]{0,49})\}\}", RegexOptions.Compiled);

    public string Render(string rawTemplate, IDictionary<string, string> payload, string language)
    {
        if (rawTemplate == null) throw new ArgumentNullException(nameof(rawTemplate));
        if (payload == null) throw new ArgumentNullException(nameof(payload));

        var langLower = language?.ToLowerInvariant() ?? string.Empty;
        if (langLower != "zpl" && langLower != "tspl")
        {
            throw new InvalidOperationException("Ngôn ngữ mẫu tem không hợp lệ. Chỉ hỗ trợ 'zpl' hoặc 'tspl'.");
        }

        // Validate max template length (32KB)
        if (rawTemplate.Length > 32 * 1024)
        {
            throw new InvalidOperationException("Kích thước mã tem thô vượt quá giới hạn tối đa 32KB.");
        }

        var missingTokens = new List<string>();

        var result = TokenRegex.Replace(rawTemplate, match =>
        {
            var key = match.Groups[1].Value;
            if (!payload.TryGetValue(key, out var rawValue) || rawValue == null)
            {
                missingTokens.Add(key);
                return match.Value; // Keep template parameter if missing to raise error later
            }

            return SanitizeValue(rawValue, langLower);
        });

        if (missingTokens.Count > 0)
        {
            throw new KeyNotFoundException($"Thiếu các trường bắt buộc trong payload: {string.Join(", ", missingTokens)}");
        }

        return result;
    }

    public static string SanitizeValue(string value, string language)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        if (language == "zpl")
        {
            // Remove ZPL control chars: ^, ~, \u001b (ESC)
            return value.Replace("^", " ").Replace("~", " ").Replace("\u001b", " ");
        }
        else if (language == "tspl")
        {
            // Remove TSPL control chars: double quote ", newline \r, \n, \u001b (ESC)
            return value.Replace("\"", " ").Replace("\r", " ").Replace("\n", " ").Replace("\u001b", " ");
        }

        return value;
    }

    public static string ComputeCommandHash(string renderedCommand)
    {
        if (string.IsNullOrEmpty(renderedCommand)) return string.Empty;
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(renderedCommand));
        var sb = new StringBuilder();
        foreach (byte b in bytes)
        {
            sb.Append(b.ToString("x2"));
        }
        return sb.ToString();
    }
}
