using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Serial.Contexts;
using Nexustock.Modules.Serial.DTOs;
using Nexustock.Modules.Serial.Entities;
using Nexustock.Modules.MasterData.Contexts;

namespace Nexustock.Modules.Serial.Services;

public class SerialService : ISerialService
{
    private readonly SerialDbContext _db;
    private readonly MasterDataDbContext _masterDb;
    private readonly ITenantProvider _tenantProvider;

    public SerialService(
        SerialDbContext db, 
        MasterDataDbContext masterDb, 
        ITenantProvider tenantProvider)
    {
        _db = db;
        _masterDb = masterDb;
        _tenantProvider = tenantProvider;
    }

    public async Task<SerialDto> ReceiveSerialAsync(ReceiveSerialDto dto, string operatorName)
    {
        var tenantId = _tenantProvider.TenantId;

        // 1. Kiểm tra sản phẩm có quản lý serial không
        var product = await _masterDb.Products.FirstOrDefaultAsync(p => p.Id == dto.ItemId && p.TenantId == tenantId);
        if (product == null)
            throw new ArgumentException("Sản phẩm không tồn tại trong hệ thống.");
        
        if (!product.IsSerialTracked)
            throw new InvalidOperationException("Sản phẩm này không được cấu hình để quản lý mã Serial.");

        // 2. Kiểm tra vị trí kệ có tồn tại không
        var location = await _masterDb.StorageLocations.FirstOrDefaultAsync(l => l.Id == dto.LocationId && l.TenantId == tenantId);
        if (location == null)
            throw new ArgumentException("Vị trí kệ không tồn tại.");

        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 3. Kiểm tra trùng lặp serial đang hoạt động
            var existing = await _db.SerialNumbers
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == dto.ItemId && s.SerialNo == dto.SerialNo);
            if (existing != null && existing.Status != "SHIPPED")
                throw new InvalidOperationException($"Mã serial {dto.SerialNo} đã tồn tại trong kho với trạng thái {existing.Status}.");

            var serial = new SerialNumber
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ItemId = dto.ItemId,
                SerialNo = dto.SerialNo,
                LocationId = dto.LocationId,
                Status = "RECEIVED",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };

            await _db.SerialNumbers.AddAsync(serial);

            var serialEvent = new SerialEvent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                SerialId = serial.Id,
                EventType = "RECEIVE",
                ToLocationId = dto.LocationId,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = operatorName
            };

            await _db.SerialEvents.AddAsync(serialEvent);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return MapToDto(serial);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> ValidateSerialForPickAsync(ValidateSerialDto dto)
    {
        var tenantId = _tenantProvider.TenantId;

        var serial = await _db.SerialNumbers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == dto.ItemId && s.SerialNo == dto.SerialNo);

        if (serial == null)
            throw new KeyNotFoundException("Mã serial không tồn tại trong hệ thống.");

        if (serial.Status != "ACTIVE" && serial.Status != "RECEIVED")
            throw new InvalidOperationException($"Trạng thái mã serial không hợp lệ để lấy hàng. Trạng thái hiện tại: {serial.Status}");

        if (serial.LocationId != dto.CurrentLocationId)
            throw new InvalidOperationException($"Mã serial nằm ở vị trí kệ khác với yêu cầu. Vị trí thực tế: {serial.LocationId}");

        return true;
    }

    public async Task<List<SerialDto>> ImportFromCsvAsync(Stream csvStream, Guid itemId, Guid locationId, string operatorName)
    {
        var tenantId = _tenantProvider.TenantId;

        // Kiểm tra master data trước
        var product = await _masterDb.Products.FirstOrDefaultAsync(p => p.Id == itemId && p.TenantId == tenantId);
        if (product == null || !product.IsSerialTracked)
            throw new ArgumentException("Sản phẩm không hợp lệ hoặc không áp dụng Serial.");

        var location = await _masterDb.StorageLocations.FirstOrDefaultAsync(l => l.Id == locationId && l.TenantId == tenantId);
        if (location == null)
            throw new ArgumentException("Vị trí kệ không hợp lệ.");

        // Đọc CSV
        var serialsToInsert = new List<string>();
        using (var reader = new StreamReader(csvStream, Encoding.UTF8))
        {
            var header = await reader.ReadLineAsync(); // Bỏ qua header
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(line)) continue;

                var columns = line.Split(',');
                if (columns.Length > 0 && !string.IsNullOrWhiteSpace(columns[0]))
                {
                    serialsToInsert.Add(columns[0].Trim());
                }
            }
        }

        if (!serialsToInsert.Any())
            throw new ArgumentException("File CSV không chứa mã serial nào.");

        var result = new List<SerialDto>();
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var serialNo in serialsToInsert)
            {
                // Kiểm tra trùng lặp
                var existing = await _db.SerialNumbers
                    .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.ItemId == itemId && s.SerialNo == serialNo);
                if (existing != null && existing.Status != "SHIPPED")
                    throw new InvalidOperationException($"Mã serial {serialNo} đã tồn tại trong kho.");

                var serial = new SerialNumber
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    ItemId = itemId,
                    SerialNo = serialNo,
                    LocationId = locationId,
                    Status = "RECEIVED",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = operatorName
                };

                await _db.SerialNumbers.AddAsync(serial);

                var serialEvent = new SerialEvent
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    SerialId = serial.Id,
                    EventType = "RECEIVE",
                    ToLocationId = locationId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = operatorName
                };

                await _db.SerialEvents.AddAsync(serialEvent);
                result.Add(MapToDto(serial));
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<List<object>> GetSerialTimelineAsync(string serialNo)
    {
        var tenantId = _tenantProvider.TenantId;

        var serial = await _db.SerialNumbers
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.SerialNo == serialNo);
        if (serial == null)
            throw new KeyNotFoundException("Mã serial không tồn tại.");

        var events = await _db.SerialEvents
            .Where(e => e.TenantId == tenantId && e.SerialId == serial.Id)
            .OrderBy(e => e.CreatedAt)
            .ToListAsync();

        return events.Select(e => new
        {
            e.Id,
            e.EventType,
            e.FromLocationId,
            e.ToLocationId,
            e.ReferenceId,
            e.CreatedAt,
            e.CreatedBy
        }).Cast<object>().ToList();
    }

    private static SerialDto MapToDto(SerialNumber serial)
    {
        return new SerialDto
        {
            Id = serial.Id,
            ItemId = serial.ItemId,
            SerialNo = serial.SerialNo,
            LocationId = serial.LocationId,
            Status = serial.Status,
            CreatedAt = serial.CreatedAt,
            CreatedBy = serial.CreatedBy
        };
    }
}
