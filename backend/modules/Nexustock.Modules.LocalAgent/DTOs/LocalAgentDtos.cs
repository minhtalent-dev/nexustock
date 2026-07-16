using System;
using System.Collections.Generic;

namespace Nexustock.Modules.LocalAgent.DTOs;

public class GeneratePairingCodeRequestDto
{
    public string StationCode { get; set; } = null!;
    public string Name { get; set; } = null!;
}

public class PairingCodeResponseDto
{
    public string PairingCode { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
}

public class ConfirmPairRequestDto
{
    public string StationCode { get; set; } = null!;
    public string PairingCode { get; set; } = null!;
    public string MachineName { get; set; } = null!;
}

public class ConfirmPairResponseDto
{
    public Guid StationId { get; set; }
    public string AgentToken { get; set; } = null!;
}

public class DeviceHeartbeatDto
{
    public string DeviceId { get; set; } = null!;
    public string DeviceType { get; set; } = null!;
    public string ConnectionState { get; set; } = null!;
    public string? LastErrorMessage { get; set; }
}

public class HeartbeatRequestDto
{
    public List<DeviceHeartbeatDto> Devices { get; set; } = new();
}

public class HeartbeatResponseDto
{
    public string Status { get; set; } = "active";
}

public class RevokeStationRequestDto
{
    public string ReasonCode { get; set; } = null!;
    public string? Description { get; set; }
}

public class StationDeviceDto
{
    public string DeviceId { get; set; } = null!;
    public string DeviceType { get; set; } = null!;
    public string ConnectionState { get; set; } = null!;
    public DateTime LastHeartbeatAt { get; set; }
    public string? LastErrorMessage { get; set; }
}

public class StationResponseDto
{
    public Guid StationId { get; set; }
    public string StationCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string? MachineName { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public List<StationDeviceDto> Devices { get; set; } = new();
}

public class PaginatedListDto<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
