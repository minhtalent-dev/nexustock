using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nexustock.LocalAgent;

public class WebSocketMessage
{
    [JsonPropertyName("messageId")]
    public string MessageId { get; set; } = null!;

    [JsonPropertyName("type")]
    public string Type { get; set; } = null!;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = null!; // ISO 8601

    [JsonPropertyName("payload")]
    public JsonElement Payload { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}

public class ErrorResponsePayload
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = null!;

    [JsonPropertyName("message")]
    public string Message { get; set; } = null!;
}
