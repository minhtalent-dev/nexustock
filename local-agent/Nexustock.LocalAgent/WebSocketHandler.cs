using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nexustock.LocalAgent.Devices.Scale;

namespace Nexustock.LocalAgent;

public class WebSocketHandler
{
    private readonly ILogger<WebSocketHandler> _logger;
    private readonly HttpClient _httpClient;
    private readonly IScaleDevice _scaleDevice;

    public WebSocketHandler(ILogger<WebSocketHandler> logger, IScaleDevice scaleDevice)
    {
        _logger = logger;
        _scaleDevice = scaleDevice;
        _httpClient = new HttpClient();
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, HttpContext context)
    {
        var buffer = new byte[1024 * 4];
        var config = ConfigManager.Load();

        while (webSocket.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            using var ms = new MemoryStream();
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                break;
            }

            if (result.MessageType == WebSocketMessageType.Text)
            {
                ms.Seek(0, SeekOrigin.Begin);
                using var reader = new StreamReader(ms, Encoding.UTF8);
                var rawMessage = await reader.ReadToEndAsync();

                await ProcessMessageAsync(webSocket, rawMessage, config);
            }
        }
    }

    private async Task ProcessMessageAsync(WebSocket webSocket, string rawMessage, AgentConfig config)
    {
        WebSocketMessage? msg = null;
        try
        {
            msg = JsonSerializer.Deserialize<WebSocketMessage>(rawMessage);
        }
        catch (Exception ex)
        {
            await SendErrorAsync(webSocket, "unknown", "agent.invalid_format", $"Sai định dạng message JSON: {ex.Message}");
            return;
        }

        if (msg == null || string.IsNullOrEmpty(msg.MessageId) || string.IsNullOrEmpty(msg.Type) || string.IsNullOrEmpty(msg.Timestamp))
        {
            await SendErrorAsync(webSocket, "unknown", "agent.invalid_format", "Thiếu các trường bắt buộc messageId, type hoặc timestamp.");
            return;
        }

        if (!WebSocketSecurity.HasValidTimestamp(msg.Timestamp, out var errorCode, out var errorMessage))
        {
            await SendErrorAsync(webSocket, msg.MessageId, errorCode, errorMessage);
            return;
        }

        switch (msg.Type)
        {
            case "agent.status.request":
                await HandleStatusRequestAsync(webSocket, msg, config);
                break;

            case "agent.pair.request":
                await HandlePairRequestAsync(webSocket, msg, config);
                break;

            case "agent.command.ping":
                await HandlePingCommandAsync(webSocket, msg, config);
                break;

            case "agent.reset.request":
                await HandleResetRequestAsync(webSocket, msg, config);
                break;

            case "scale.status.request":
                await HandleScaleStatusRequestAsync(webSocket, msg);
                break;

            case "scale.weight.subscribe":
                await HandleScaleSubscribeAsync(webSocket, msg);
                break;

            case "scale.zero.request":
                await HandleScaleZeroAsync(webSocket, msg, config);
                break;

            case "scale.tare.request":
                await HandleScaleTareAsync(webSocket, msg, config);
                break;

            default:
                await SendErrorAsync(webSocket, msg.MessageId, "agent.unknown_type", $"Không hỗ trợ message type: {msg.Type}");
                break;
        }
    }

    private async Task HandleStatusRequestAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        var isPaired = IsAgentPaired(config);
        var responsePayload = new
        {
            stationId = config.StationId,
            stationCode = config.StationCode,
            status = isPaired ? "paired" : "unpaired",
            webSocketPort = config.WebSocketPort,
            allowedOrigins = config.AllowedOrigins
        };

        await SendResponseAsync(webSocket, msg.MessageId, "agent.status.response", responsePayload);
    }

    private async Task HandleScaleStatusRequestAsync(WebSocket webSocket, WebSocketMessage msg)
    {
        await SendResponseAsync(webSocket, msg.MessageId, "scale.status.response", ToScalePayload(_scaleDevice.Current));
    }

    private async Task HandleScaleSubscribeAsync(WebSocket webSocket, WebSocketMessage msg)
    {
        await SendResponseAsync(webSocket, msg.MessageId, "scale.weightChanged", ToScalePayload(_scaleDevice.Current));
    }

    private async Task HandleScaleZeroAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (!await EnsureSignedScaleCommandAsync(webSocket, msg, config)) return;
        await _scaleDevice.ZeroAsync(CancellationToken.None);
        await SendResponseAsync(webSocket, msg.MessageId, "scale.zero.response", new { success = true });
    }

    private async Task HandleScaleTareAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (!await EnsureSignedScaleCommandAsync(webSocket, msg, config)) return;
        await _scaleDevice.TareAsync(CancellationToken.None);
        await SendResponseAsync(webSocket, msg.MessageId, "scale.tare.response", new { success = true });
    }

    private async Task<bool> EnsureSignedScaleCommandAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (!IsAgentPaired(config))
        {
            await SendErrorAsync(webSocket, msg.MessageId, "agent.unpaired", "Trạm chưa được ghép cặp.");
            return false;
        }

        if (!WebSocketSecurity.VerifySignedPayload(msg, config, out var errorCode, out var errorMessage))
        {
            await SendErrorAsync(webSocket, msg.MessageId, errorCode, errorMessage);
            return false;
        }

        return true;
    }

    private static object ToScalePayload(ScaleReading reading)
    {
        return new
        {
            deviceId = reading.DeviceId,
            weightKg = reading.WeightKg,
            stable = reading.Stable,
            rawFrame = reading.RawFrame,
            profile = reading.Profile,
            connectionState = reading.ConnectionState,
            errorCode = reading.ErrorCode,
            timestamp = reading.Timestamp.ToString("o")
        };
    }

    private async Task HandlePairRequestAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (IsAgentPaired(config))
        {
            await SendErrorAsync(webSocket, msg.MessageId, "agent.already_paired", "Agent đã được ghép cặp từ trước.");
            return;
        }

        string? stationCode = null;
        string? pairingCode = null;
        try
        {
            if (msg.Payload.ValueKind == JsonValueKind.Object)
            {
                if (msg.Payload.TryGetProperty("stationCode", out var scProp)) stationCode = scProp.GetString();
                if (msg.Payload.TryGetProperty("pairingCode", out var pcProp)) pairingCode = pcProp.GetString();
            }
        }
        catch
        {
        }

        if (string.IsNullOrEmpty(stationCode) || string.IsNullOrEmpty(pairingCode))
        {
            await SendErrorAsync(webSocket, msg.MessageId, "agent.invalid_payload", "Thiếu stationCode hoặc pairingCode trong payload.");
            return;
        }

        var backendUrl = $"{config.BackendBaseUrl.TrimEnd('/')}/api/agent/stations/confirm-pair";
        var confirmPayload = new
        {
            stationCode = stationCode,
            pairingCode = pairingCode,
            machineName = Environment.MachineName
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync(backendUrl, confirmPayload);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                await SendErrorAsync(webSocket, msg.MessageId, "backend.failed", $"Backend từ chối ghép cặp: {errContent}");
                return;
            }

            var confirmResult = await response.Content.ReadFromJsonAsync<ConfirmResponseDto>();
            if (confirmResult == null || string.IsNullOrEmpty(confirmResult.AgentToken))
            {
                await SendErrorAsync(webSocket, msg.MessageId, "backend.failed", "Phản hồi xác nhận ghép cặp không chứa token.");
                return;
            }

            var scope = Enum.Parse<DataProtectionScope>(config.DpapiScope);
            var encryptedToken = DpapiSecretStorage.Encrypt(confirmResult.AgentToken, scope);

            config.StationId = confirmResult.StationId;
            config.StationCode = stationCode;
            config.EncryptedAgentToken = encryptedToken;

            ConfigManager.Save(config);

            _logger.LogInformation($"Ghép cặp trạm {stationCode} thành công.");

            await SendResponseAsync(webSocket, msg.MessageId, "agent.pair.response", new
            {
                stationId = config.StationId,
                status = "paired"
            });
        }
        catch (Exception ex)
        {
            await SendErrorAsync(webSocket, msg.MessageId, "agent.exception", $"Lỗi ghép cặp: {ex.Message}");
        }
    }

    private async Task HandlePingCommandAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (!await EnsureSignedScaleCommandAsync(webSocket, msg, config)) return;
        await SendResponseAsync(webSocket, msg.MessageId, "agent.command.pong", new { });
    }

    private async Task HandleResetRequestAsync(WebSocket webSocket, WebSocketMessage msg, AgentConfig config)
    {
        if (!await EnsureSignedScaleCommandAsync(webSocket, msg, config)) return;

        config.StationId = null;
        config.StationCode = null;
        config.EncryptedAgentToken = null;
        ConfigManager.Save(config);

        _logger.LogWarning("Trạm đã bị reset cấu hình cục bộ theo yêu cầu.");

        await SendResponseAsync(webSocket, msg.MessageId, "agent.reset.response", new { status = "unpaired" });
    }

    private static bool IsAgentPaired(AgentConfig config)
    {
        return config.StationId.HasValue && !string.IsNullOrEmpty(config.EncryptedAgentToken);
    }

    private static async Task SendResponseAsync(WebSocket webSocket, string messageId, string type, object payload)
    {
        var response = new
        {
            messageId = messageId,
            type = type,
            timestamp = DateTime.UtcNow.ToString("o"),
            payload = payload
        };

        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(response);
        await webSocket.SendAsync(new ArraySegment<byte>(jsonBytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task SendErrorAsync(WebSocket webSocket, string messageId, string code, string message)
    {
        var errPayload = new ErrorResponsePayload
        {
            Code = code,
            Message = message
        };

        await SendResponseAsync(webSocket, messageId, "agent.error", errPayload);
    }

    private class ConfirmResponseDto
    {
        public Guid StationId { get; set; }
        public string AgentToken { get; set; } = null!;
    }
}
