using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Putaway.Contexts;
using Nexustock.Modules.Putaway.Dtos;
using Nexustock.Modules.Putaway.Entities;
using Nexustock.Modules.MasterData.Contexts;
using Nexustock.Modules.Inventory.Contexts;
using Nexustock.Modules.Rules.Services;

namespace Nexustock.Modules.Putaway.Services;

public interface IPutawayService
{
    Task<PutawayProposalResponseDto> GenerateProposalsAsync(Guid tenantId, Guid lotId, decimal qty, string username);
}

public class PutawayService : IPutawayService
{
    private readonly PutawayDbContext _context;
    private readonly MasterDataDbContext _masterContext;
    private readonly InventoryDbContext _inventoryContext;
    private readonly IRuleEvaluator _ruleEvaluator;

    public PutawayService(
        PutawayDbContext context,
        MasterDataDbContext masterContext,
        InventoryDbContext inventoryContext,
        IRuleEvaluator ruleEvaluator)
    {
        _context = context;
        _masterContext = masterContext;
        _inventoryContext = inventoryContext;
        _ruleEvaluator = ruleEvaluator;
    }

    public async Task<PutawayProposalResponseDto> GenerateProposalsAsync(Guid tenantId, Guid lotId, decimal qty, string username)
    {
        // 1. Fetch Lot information
        var lot = await _inventoryContext.Lots
            .FirstOrDefaultAsync(l => l.Id == lotId && l.TenantId == tenantId);
        if (lot == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy lô hàng với ID '{lotId}'");
        }

        // 2. Fetch Product & Config
        var product = await _masterContext.Products
            .FirstOrDefaultAsync(p => p.Id == lot.ItemId && p.TenantId == tenantId);
        if (product == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vật tư cho lô hàng này");
        }

        var productConfig = await _masterContext.ProductConfigs
            .FirstOrDefaultAsync(pc => pc.ProductId == lot.ItemId && pc.TenantId == tenantId);

        // 3. Find source location of the lot (where qty > 0)
        var sourceBalance = await _inventoryContext.Inventories
            .Where(i => i.TenantId == tenantId && i.ItemId == lot.ItemId && i.LotNo == lot.LotNo && i.QtyOnHand > 0)
            .FirstOrDefaultAsync();

        int startX = 0, startY = 0, startZ = 0;
        if (sourceBalance != null)
        {
            var sourceLoc = await _masterContext.StorageLocations.FindAsync(sourceBalance.LocationId);
            if (sourceLoc != null)
            {
                startX = sourceLoc.XCoord;
                startY = sourceLoc.YCoord;
                startZ = sourceLoc.ZCoord;
            }
        }

        // 4. Fetch all active candidate locations
        var locations = await _masterContext.StorageLocations
            .Where(l => l.TenantId == tenantId && l.IsActive)
            .ToListAsync();

        var zoneIds = locations.Select(l => l.ZoneId).Distinct().ToList();
        var zones = await _masterContext.StorageZones
            .Where(z => z.TenantId == tenantId && zoneIds.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z);

        // 5. Fetch location locks (Inbound or All)
        var inboundLockedLocs = await _inventoryContext.LocationLocks
            .Where(l => l.TenantId == tenantId && (l.LockType == "INBOUND" || l.LockType == "ALL"))
            .Select(l => l.LocationId)
            .ToListAsync();

        // 6. Fetch occupancies of all locations dynamically to avoid N+1 queries
        var occupancies = await _inventoryContext.Inventories
            .Where(i => i.TenantId == tenantId)
            .GroupBy(i => i.LocationId)
            .Select(g => new { LocationId = g.Key, Qty = g.Sum(i => i.QtyOnHand) })
            .ToDictionaryAsync(x => x.LocationId, x => x.Qty);

        var proposals = new List<PutawayCandidateDto>();
        var candidatesToSave = new List<PutawayProposal>();

        // Remove old proposals for this lot to keep it fresh
        var oldProposals = await _context.PutawayProposals
            .Where(p => p.TenantId == tenantId && p.LotId == lotId && p.Status == "SUGGESTED")
            .ToListAsync();
        _context.PutawayProposals.RemoveRange(oldProposals);
        await _context.SaveChangesAsync();

        foreach (var loc in locations)
        {
            // Filter 1: Exclude locked locations
            if (inboundLockedLocs.Contains(loc.Id)) continue;

            // Filter 2: Capacity check
            var currentQty = occupancies.TryGetValue(loc.Id, out var oQty) ? oQty : 0m;
            if (currentQty + qty > loc.MaxCapacity) continue;

            // Filter 3: Rule engine evaluation
            var zoneObj = zones.TryGetValue(loc.ZoneId, out var zObj) ? zObj : null;
            var locZoneCode = zoneObj?.Code ?? string.Empty;
            var warehouseId = zoneObj?.WarehouseId ?? Guid.Empty;
            
            var evalContext = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "productCode", product.Code },
                { "productGroup", product.Code },
                { "locationZone", locZoneCode },
                { "weightClass", productConfig?.WeightClass ?? "MEDIUM" },
                { "rotationSpeed", productConfig?.RotationSpeed ?? "SLOW" }
            };

            var ruleResult = await _ruleEvaluator.EvaluateAsync(tenantId, "PUTAWAY", evalContext, username);
            if (ruleResult.Matched && ruleResult.ActionType == "BLOCK")
            {
                // Rule blocked this location
                continue;
            }

            // 7. Calculate Proximity and Scoring
            int score = 0;
            var reasons = new List<string>();

            // Criteria A: Zone preference match
            // For Putaway, if Rule Engine proposes location or zone via parameters, we can parse it,
            // or if it matches certain positive rules (ALLOW/WARN), we grant baseline points.
            // Let's assume standard positive zone mapping rules or check product prefix.
            // Also if productGroup is mapped to a specific zone by rule:
            // If the rule matched and is ALLOW, we can extract details
            if (ruleResult.Matched && ruleResult.ActionType == "ALLOW")
            {
                score += 50;
                reasons.Add("Khớp luật ưu tiên vùng cất (+50)");
            }

            // Criteria B: Compatibility (Contains same product)
            var hasSameProduct = await _inventoryContext.Inventories
                .AnyAsync(i => i.TenantId == tenantId && i.LocationId == loc.Id && i.ItemId == lot.ItemId && i.QtyOnHand > 0);
            if (hasSameProduct)
            {
                score += 30;
                reasons.Add("Đang chứa cùng loại vật tư (+30)");
            }

            // Criteria C: Empty location bonus
            if (currentQty == 0)
            {
                score += 10;
                reasons.Add("Vị trí kệ trống (+10)");
            }

            // Criteria D: Manhattan Proximity
            int distance = Math.Abs(loc.XCoord - startX) + Math.Abs(loc.YCoord - startY) + Math.Abs(loc.ZCoord - startZ);
            int distanceScore = Math.Max(0, 10 - distance);
            if (distanceScore > 0)
            {
                score += distanceScore;
                reasons.Add($"Tiện lợi lối đi (+{distanceScore})");
            }

            var reasonText = reasons.Count > 0 ? string.Join(", ", reasons) : "Đề xuất mặc định";

            var propId = Guid.NewGuid();

            proposals.Add(new PutawayCandidateDto
            {
                ProposalId = propId,
                LocationId = loc.Id,
                LocationCode = loc.Code,
                ZoneCode = locZoneCode,
                Score = score,
                Reason = reasonText
            });

            // Prepare database record
            candidatesToSave.Add(new PutawayProposal
            {
                Id = propId,
                TenantId = tenantId,
                WarehouseId = warehouseId,
                LotId = lotId,
                ItemId = lot.ItemId,
                Qty = qty,
                CandidateLocationId = loc.Id,
                Score = score,
                Reason = reasonText,
                Status = "SUGGESTED",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            });
        }

        // Save suggested proposals to database
        if (candidatesToSave.Count > 0)
        {
            _context.PutawayProposals.AddRange(candidatesToSave);
            await _context.SaveChangesAsync();
        }

        // Sort proposals by score descending
        var sortedProposals = proposals.OrderByDescending(p => p.Score).ToList();

        // 8. Generate entire layout of the zones for 2D Grid map
        var targetZoneIds = proposals.Select(p => locations.First(loc => loc.Id == p.LocationId).ZoneId).Distinct().ToList();
        var zoneLocDtos = locations
            .Where(l => targetZoneIds.Contains(l.ZoneId))
            .Select(l => {
                string status = "FREE";
                if (sortedProposals.Any(p => p.LocationId == l.Id))
                {
                    status = "PROPOSED";
                }
                else if (occupancies.TryGetValue(l.Id, out var oQ) && oQ > 0)
                {
                    status = "OCCUPIED";
                }
                return new ZoneLocationDto
                {
                    LocationId = l.Id,
                    LocationCode = l.Code,
                    X = l.XCoord,
                    Y = l.YCoord,
                    Z = l.ZCoord,
                    Status = status
                };
            })
            .ToList();

        return new PutawayProposalResponseDto
        {
            LotId = lot.Id,
            ItemId = lot.ItemId,
            ItemCode = product.Code,
            ItemName = product.Name,
            LotNo = lot.LotNo,
            Qty = qty,
            Proposals = sortedProposals,
            ZoneLocations = zoneLocDtos
        };
    }
}
