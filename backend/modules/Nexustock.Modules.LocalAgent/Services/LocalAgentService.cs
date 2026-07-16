using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.LocalAgent.Contexts;
using Nexustock.Modules.LocalAgent.DTOs;
using Nexustock.Modules.LocalAgent.Entities;

namespace Nexustock.Modules.LocalAgent.Services;

public class LocalAgentService : ILocalAgentService
{
    private readonly LocalAgentDbContext _dbContext;

    public LocalAgentService(LocalAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PairingCodeResponseDto> GeneratePairingCodeAsync(Guid tenantId, string username, GeneratePairingCodeRequestDto dto)
    {
        // 1. Tạo OTP 6 chữ số
        var codeInt = RandomNumberGenerator.GetInt32(100000, 1000000);
        var plainCode = codeInt.ToString();
        var codeHash = ComputeSha256(plainCode);
        var expiresAt = DateTime.UtcNow.AddMinutes(3);

        // 2. Vô hiệu hóa các mã cũ chưa dùng của StationCode này
        var oldCodes = await _dbContext.AgentPairingCodes
            .Where(x => x.TenantId == tenantId && x.StationCode == dto.StationCode && x.ConsumedAt == null)
            .ToListAsync();
        foreach (var old in oldCodes)
        {
            old.ConsumedAt = DateTime.UtcNow; // Mark as consumed to invalidate
        }

        // 3. Tạo mới pairing code record
        var pairingCodeEntity = new AgentPairingCode
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StationCode = dto.StationCode,
            CodeHash = codeHash,
            ExpiresAt = expiresAt,
            CreatedBy = username,
            CreatedAt = DateTime.UtcNow,
            InvalidAttempts = 0,
            IsLocked = false
        };

        _dbContext.AgentPairingCodes.Add(pairingCodeEntity);
        await _dbContext.SaveChangesAsync();

        return new PairingCodeResponseDto
        {
            PairingCode = plainCode,
            ExpiresAt = expiresAt
        };
    }

    public async Task<ConfirmPairResponseDto> ConfirmPairAsync(ConfirmPairRequestDto dto)
    {
        // Public API: Cần tìm code trên mọi tenant
        var activeCode = await _dbContext.AgentPairingCodes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.StationCode == dto.StationCode && x.ConsumedAt == null && x.ExpiresAt > DateTime.UtcNow);

        if (activeCode == null)
        {
            throw new InvalidOperationException("Mã ghép cặp không hợp lệ hoặc đã hết hạn.");
        }

        if (activeCode.IsLocked)
        {
            throw new InvalidOperationException("Mã ghép cặp đã bị khóa do thử sai quá nhiều lần.");
        }

        var hash = ComputeSha256(dto.PairingCode);
        if (activeCode.CodeHash != hash)
        {
            activeCode.InvalidAttempts++;
            if (activeCode.InvalidAttempts >= 5)
            {
                activeCode.IsLocked = true;
            }
            await _dbContext.SaveChangesAsync();

            // Ghi audit log thất bại
            var failEvent = new AgentConnectionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = activeCode.TenantId,
                EventType = "pairingRejected",
                MachineName = dto.MachineName,
                Message = $"Ghép cặp thất bại cho trạm {dto.StationCode} (Thử sai lần thứ {activeCode.InvalidAttempts})",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AgentConnectionEvents.Add(failEvent);
            await _dbContext.SaveChangesAsync();

            throw new InvalidOperationException("Mã ghép cặp không chính xác.");
        }

        // Tạo AgentToken ngẫu nhiên độ an toàn cao
        var tokenBytes = new byte[32];
        RandomNumberGenerator.Fill(tokenBytes);
        var plainToken = Convert.ToBase64String(tokenBytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", ""); // Sanitize raw base64 string
        var tokenHash = ComputeSha256(plainToken);

        // Kiểm tra trạm làm việc đã có chưa
        var station = await _dbContext.AgentStations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == activeCode.TenantId && x.StationCode == dto.StationCode);

        if (station == null)
        {
            station = new AgentStation
            {
                Id = Guid.NewGuid(),
                TenantId = activeCode.TenantId,
                StationCode = dto.StationCode,
                Name = dto.StationCode,
                TokenHash = tokenHash,
                Status = "active",
                MachineName = dto.MachineName,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AgentStations.Add(station);
        }
        else
        {
            station.TokenHash = tokenHash;
            station.Status = "active";
            station.MachineName = dto.MachineName;
            station.UpdatedAt = DateTime.UtcNow;
        }

        activeCode.ConsumedAt = DateTime.UtcNow;

        // Ghi connection event
        var successEvent = new AgentConnectionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = activeCode.TenantId,
            StationId = station.Id,
            EventType = "paired",
            MachineName = dto.MachineName,
            Message = $"Ghép cặp thành công trạm {dto.StationCode}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AgentConnectionEvents.Add(successEvent);

        await _dbContext.SaveChangesAsync();

        return new ConfirmPairResponseDto
        {
            StationId = station.Id,
            AgentToken = plainToken
        };
    }

    public async Task<HeartbeatResponseDto> HeartbeatAsync(Guid tenantId, Guid stationId, string token, HeartbeatRequestDto dto)
    {
        var station = await _dbContext.AgentStations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == stationId);

        if (station == null)
        {
            throw new KeyNotFoundException("Không tìm thấy trạm làm việc.");
        }

        if (station.Status == "revoked")
        {
            // Log event revoked
            var revokedEvent = new AgentConnectionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StationId = stationId,
                EventType = "tokenRejected",
                MachineName = station.MachineName,
                Message = $"Từ chối heartbeat do trạm {station.StationCode} đã bị thu hồi.",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AgentConnectionEvents.Add(revokedEvent);
            await _dbContext.SaveChangesAsync();

            throw new UnauthorizedAccessException("Trạm làm việc đã bị thu hồi quyền truy cập.");
        }

        // Xác thực token băm
        var incomingHash = ComputeSha256(token);
        if (station.TokenHash != incomingHash)
        {
            var rejectEvent = new AgentConnectionEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                StationId = stationId,
                EventType = "tokenRejected",
                MachineName = station.MachineName,
                Message = $"Sai mã token xác thực từ trạm {station.StationCode}",
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.AgentConnectionEvents.Add(rejectEvent);
            await _dbContext.SaveChangesAsync();

            throw new UnauthorizedAccessException("Token xác thực trạm không hợp lệ.");
        }

        // Cập nhật trạng thái thiết bị
        var existingDevices = await _dbContext.DeviceStatuses
            .Where(x => x.StationId == stationId)
            .ToListAsync();

        foreach (var devDto in dto.Devices)
        {
            var device = existingDevices.FirstOrDefault(x => x.DeviceId == devDto.DeviceId);
            if (device == null)
            {
                device = new DeviceStatus
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StationId = stationId,
                    DeviceId = devDto.DeviceId,
                    DeviceType = devDto.DeviceType,
                    ConnectionState = devDto.ConnectionState,
                    LastHeartbeatAt = DateTime.UtcNow,
                    LastErrorMessage = devDto.LastErrorMessage
                };
                _dbContext.DeviceStatuses.Add(device);
            }
            else
            {
                device.ConnectionState = devDto.ConnectionState;
                device.LastHeartbeatAt = DateTime.UtcNow;
                device.LastErrorMessage = devDto.LastErrorMessage;
                _dbContext.Entry(device).State = EntityState.Modified;
            }
        }

        station.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return new HeartbeatResponseDto { Status = "active" };
    }

    public async Task<PaginatedListDto<StationResponseDto>> GetStationsAsync(Guid tenantId, int page, int pageSize, string? search)
    {
        var query = _dbContext.AgentStations.AsQueryable(); // query filter tự áp dụng tenantId
        
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x => x.StationCode.Contains(search) || x.Name.Contains(search));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(x => x.StationCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var stationIds = items.Select(x => x.Id).ToList();
        var devices = await _dbContext.DeviceStatuses
            .Where(x => stationIds.Contains(x.StationId))
            .ToListAsync();

        var responseItems = items.Select(st => new StationResponseDto
        {
            StationId = st.Id,
            StationCode = st.StationCode,
            Name = st.Name,
            Status = st.Status,
            MachineName = st.MachineName,
            LastHeartbeatAt = devices.Where(d => d.StationId == st.Id).Max(d => (DateTime?)d.LastHeartbeatAt),
            Devices = devices.Where(d => d.StationId == st.Id).Select(d => new StationDeviceDto
            {
                DeviceId = d.DeviceId,
                DeviceType = d.DeviceType,
                ConnectionState = d.ConnectionState,
                LastHeartbeatAt = d.LastHeartbeatAt,
                LastErrorMessage = d.LastErrorMessage
            }).ToList()
        }).ToList();

        return new PaginatedListDto<StationResponseDto>
        {
            Items = responseItems,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task RevokeStationAsync(Guid tenantId, Guid stationId, RevokeStationRequestDto dto)
    {
        var station = await _dbContext.AgentStations.FirstOrDefaultAsync(x => x.Id == stationId);
        if (station == null)
        {
            throw new KeyNotFoundException("Không tìm thấy trạm làm việc.");
        }

        station.Status = "revoked";
        station.UpdatedAt = DateTime.UtcNow;

        var revokeEvent = new AgentConnectionEvent
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            StationId = stationId,
            EventType = "revoked",
            MachineName = station.MachineName,
            Message = $"Thu hồi quyền trạm. Lý do: {dto.ReasonCode}. Chi tiết: {dto.Description}",
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AgentConnectionEvents.Add(revokeEvent);

        await _dbContext.SaveChangesAsync();
    }

    private static string ComputeSha256(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
