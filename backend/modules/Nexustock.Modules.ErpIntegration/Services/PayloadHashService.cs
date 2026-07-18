using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Nexustock.Modules.ErpIntegration.Services;

public class PayloadHashService : IPayloadHashService
{
    public string ComputeHash(string jsonPayload)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload)) return string.Empty;

        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                WriteCanonical(doc.RootElement, writer);
            }

            var canonicalBytes = ms.ToArray();
            var hashBytes = SHA256.HashData(canonicalBytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch (JsonException)
        {
            // Fallback for invalid JSON
            var bytes = Encoding.UTF8.GetBytes(jsonPayload.Trim());
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                // Sort object properties by name (ordinal)
                foreach (var prop in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteCanonical(prop.Value, writer);
                }
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonical(item, writer);
                }
                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
