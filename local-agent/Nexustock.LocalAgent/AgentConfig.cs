using System;
using System.Collections.Generic;

namespace Nexustock.LocalAgent;

public class AgentConfig
{
    public Guid? StationId { get; set; }
    public string? StationCode { get; set; }
    public string BackendBaseUrl { get; set; } = "http://localhost:5000"; // Default dev endpoint
    public int WebSocketPort { get; set; } = 9000;
    public string DpapiScope { get; set; } = "LocalMachine"; // "LocalMachine" hoặc "CurrentUser"
    public string? EncryptedAgentToken { get; set; }
    public string? CertificateThumbprint { get; set; }
    public List<string> AllowedOrigins { get; set; } = new() { "http://localhost:3000", "http://localhost:3003" };
    public bool AllowInsecureWebSocket { get; set; } = false;
}
