using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Nexustock.LocalAgent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly HttpClient _httpClient;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Heartbeat background worker started.");

        int delayMs = 30000; // Mặc định 30 giây

        while (!stoppingToken.IsCancellationRequested)
        {
            var config = ConfigManager.Load();

            if (config.StationId.HasValue && !string.IsNullOrEmpty(config.EncryptedAgentToken))
            {
                await SendHeartbeatAsync(config);
            }

            try
            {
                await Task.Delay(delayMs, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task SendHeartbeatAsync(AgentConfig config)
    {
        var scope = Enum.Parse<DataProtectionScope>(config.DpapiScope);
        var token = DpapiSecretStorage.Decrypt(config.EncryptedAgentToken!, scope);

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogError("Không thể giải mã Token. Trạm tự động chuyển về unpaired.");
            config.StationId = null;
            config.StationCode = null;
            config.EncryptedAgentToken = null;
            ConfigManager.Save(config);
            return;
        }

        var url = $"{config.BackendBaseUrl.TrimEnd('/')}/api/agent/stations/{config.StationId}/heartbeat";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("X-Agent-Token", token);
        
        // Mock list thiết bị cục bộ cho Phase 20 foundation
        var payload = new
        {
            devices = new List<object>
            {
                new { deviceId = "mock_scale_01", deviceType = "scaleCom", connectionState = "connected", lastErrorMessage = (string?)null }
            }
        };
        request.Content = JsonContent.Create(payload);

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Trạm làm việc bị thu hồi quyền truy cập (403 Forbidden). Đang xóa token cục bộ...");
                config.StationId = null;
                config.StationCode = null;
                config.EncryptedAgentToken = null;
                ConfigManager.Save(config);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Token xác thực trạm bị từ chối (401 Unauthorized).");
            }
            else if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Heartbeat gửi thành công.");
            }
            else
            {
                _logger.LogWarning($"Heartbeat phản hồi thất bại: {response.StatusCode}");
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning($"Không thể kết nối đến Backend: {ex.Message}. Trạng thái: offline.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong tiến trình gửi Heartbeat.");
        }
    }
}
