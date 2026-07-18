using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nexustock.LocalAgent;
using Nexustock.LocalAgent.Devices.Scale;
using Nexustock.LocalAgent.Devices.Printer;

namespace Nexustock.LocalAgent;

public class Program
{
    public static void Main(string[] args)
    {
        ConfigManager.LoadFailed = ex => WriteEventLog(
            $"Cảnh báo: Không đọc được cấu hình Local Agent, dùng cấu hình mặc định. {ex.GetType().Name}: {ex.Message}",
            System.Diagnostics.EventLogEntryType.Warning);

        var config = ConfigManager.Load();
        var builder = WebApplication.CreateBuilder(args);

        // Đăng ký dịch vụ
        builder.Services.AddSingleton(config.Scale);
        builder.Services.AddSingleton<ScaleFrameParser>();
        builder.Services.AddSingleton<IScaleDevice>(sp =>
        {
            var scaleConfig = sp.GetRequiredService<ScaleDeviceConfig>();
            if (string.Equals(scaleConfig.Mode, "serial", StringComparison.OrdinalIgnoreCase))
            {
                return new SerialScaleDevice(scaleConfig);
            }

            return new MockScaleDevice(scaleConfig, sp.GetRequiredService<ScaleFrameParser>());
        });
        builder.Services.AddHostedService<ScaleDeviceHostedService>();

        builder.Services.AddSingleton<IPrinterQueue, PrinterQueue>();
        foreach (var printer in config.Printers)
        {
            builder.Services.AddSingleton<IPrinterDevice>(sp =>
            {
                if (string.Equals(printer.Mode, "tcp", StringComparison.OrdinalIgnoreCase))
                {
                    return new TcpRawPrinterDevice(printer);
                }

                if (string.Equals(printer.Mode, "windows", StringComparison.OrdinalIgnoreCase))
                {
                    return new WindowsRawPrinterDevice(printer);
                }

                // Mặc định: mock (dev/test)
                return new MockPrinterDevice(printer);
            });
        }

        builder.Services.AddSingleton<WebSocketHandler>();
        if (!string.Equals(Environment.GetEnvironmentVariable("NEXUSTOCK_AGENT_DISABLE_WORKER"), "true", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddHostedService<Worker>();
        }

        // Thiết lập Windows Service
        builder.Host.UseWindowsService();

        // 1. Kiểm tra SSL Certificate cho Production/Staging
        X509Certificate2? cert = null;
        if (!builder.Environment.IsDevelopment() || !config.AllowInsecureWebSocket)
        {
            if (!string.IsNullOrEmpty(config.CertificateThumbprint))
            {
                using var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                var certs = store.Certificates.Find(X509FindType.FindByThumbprint, config.CertificateThumbprint, false);
                if (certs.Count > 0)
                {
                    cert = certs[0];
                }
            }

            if (cert == null && !builder.Environment.IsDevelopment())
            {
                WriteEventLog("Lỗi: Thiếu hoặc sai chứng chỉ SSL trong môi trường Production/Staging.", System.Diagnostics.EventLogEntryType.Error);
                Environment.Exit(101); // Safe code certificate error
            }
        }

        // 2. Tìm cổng trống dải 9000-9005
        int startPort = config.WebSocketPort;
        int selectedPort = FindAvailablePort("127.0.0.1", startPort, startPort + 5);

        if (selectedPort == -1)
        {
            WriteEventLog($"Lỗi: Không thể bind cổng trong dải {startPort}-{startPort + 5}. Tất cả cổng đã bị chiếm.", System.Diagnostics.EventLogEntryType.Error);
            Environment.Exit(102); // Safe code port unavailable
        }

        config.WebSocketPort = selectedPort;
        ConfigManager.Save(config);

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.Listen(IPAddress.Loopback, selectedPort, listenOptions =>
            {
                if (cert != null)
                {
                    listenOptions.UseHttps(cert);
                }
            });
        });

        var app = builder.Build();

        app.UseWebSockets();

        // Middleware xử lý WebSocket và Origin allowlist
        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/ws")
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var origin = context.Request.Headers["Origin"].ToString();
                    bool originAllowed = false;

                    foreach (var allowed in config.AllowedOrigins)
                    {
                        if (WebSocketSecurity.MatchOrigin(origin, allowed))
                        {
                            originAllowed = true;
                            break;
                        }
                    }

                    if (!originAllowed)
                    {
                        WriteEventLog($"Từ chối kết nối WebSocket do sai Origin: {origin}", System.Diagnostics.EventLogEntryType.Warning);
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsync("Origin Denied");
                        return;
                    }

                    var handler = context.RequestServices.GetRequiredService<WebSocketHandler>();
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await handler.HandleConnectionAsync(webSocket, context);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await next(context);
            }
        });

        app.Run();
    }

    private static int FindAvailablePort(string ipAddress, int startPort, int endPort)
    {
        var ip = IPAddress.Parse(ipAddress);
        for (int port = startPort; port <= endPort; port++)
        {
            TcpListener? listener = null;
            try
            {
                listener = new TcpListener(ip, port);
                listener.Start();
                return port;
            }
            catch
            {
                // Port busy
            }
            finally
            {
                listener?.Stop();
            }
        }
        return -1;
    }

    private static void WriteEventLog(string message, System.Diagnostics.EventLogEntryType type)
    {
        try
        {
            if (!System.Diagnostics.EventLog.SourceExists("Nexustock.LocalAgent"))
            {
                System.Diagnostics.EventLog.CreateEventSource("Nexustock.LocalAgent", "Application");
            }
            System.Diagnostics.EventLog.WriteEntry("Nexustock.LocalAgent", message, type);
        }
        catch
        {
            Console.WriteLine($"EventLog [{type}]: {message}");
        }
    }
}
