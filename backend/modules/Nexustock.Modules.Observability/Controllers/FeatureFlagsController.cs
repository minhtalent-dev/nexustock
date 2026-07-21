using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;

namespace Nexustock.Modules.Observability.Controllers;

[Authorize]
[ApiController]
[Route("api/feature-flags")]
public class FeatureFlagsController : ControllerBase
{
    private readonly ObservabilityDbContext _db;

    public FeatureFlagsController(ObservabilityDbContext db)
    {
        _db = db;
    }

    public record UpdateFeatureFlagRequest(bool Enabled);

    [HttpPut("{name}")]
    public async Task<IActionResult> Update(string name, [FromBody] UpdateFeatureFlagRequest request)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { errorCode = "FEATURE_FLAG_INVALID", message = "Flag name is required." });

        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(f => f.Name == name);
        if (flag is null)
        {
            flag = new FeatureFlag
            {
                Name = name,
                Enabled = request.Enabled,
                RolloutPercentage = 100,
                WhitelistUserIds = string.Empty,
                Description = $"Auto-created via API for {name}",
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.FeatureFlags.Add(flag);
        }
        else
        {
            flag.Enabled = request.Enabled;
            flag.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Ok(new { name = flag.Name, enabled = flag.Enabled, updatedAt = flag.UpdatedAt });
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> Get(string name)
    {
        var flag = await _db.FeatureFlags.AsNoTracking().FirstOrDefaultAsync(f => f.Name == name);
        if (flag is null)
            return NotFound(new { errorCode = "FEATURE_FLAG_NOT_FOUND", message = "Flag not found." });
        return Ok(new { name = flag.Name, enabled = flag.Enabled, updatedAt = flag.UpdatedAt });
    }
}
