using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Nexustock.Modules.Observability.Contexts;
using Nexustock.Modules.Observability.Entities;

namespace Nexustock.Modules.Observability.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly ObservabilityDbContext _dbContext;
    private readonly IConfiguration _configuration;

    public FeatureFlagService(ObservabilityDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;
        _configuration = configuration;
    }

    public async Task<bool> IsEnabledAsync(string flagName, Guid? userId = null)
    {
        // 1. Env variable override (e.g. FF_ALLOCATION_V2=true/false)
        var envKey = $"FF_{flagName}";
        var envVal = _configuration[envKey];
        if (!string.IsNullOrEmpty(envVal))
        {
            if (bool.TryParse(envVal, out var parsed))
            {
                return parsed;
            }
        }

        // 2. Lookup DB
        var flag = await _dbContext.FeatureFlags
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Name == flagName);

        if (flag == null)
        {
            return false;
        }

        if (!flag.Enabled)
        {
            return false;
        }

        // 3. Whitelist User check
        if (userId.HasValue && !string.IsNullOrEmpty(flag.WhitelistUserIds))
        {
            try
            {
                var users = JsonSerializer.Deserialize<string[]>(flag.WhitelistUserIds);
                if (users != null && users.Contains(userId.Value.ToString("D"), StringComparer.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch
            {
                // Bỏ qua lỗi parse JSON whitelist
            }
        }

        // 4. Rollout percentage check
        if (flag.RolloutPercentage > 0)
        {
            if (flag.RolloutPercentage >= 100)
            {
                return true;
            }

            if (userId.HasValue)
            {
                var hash = GetDeterministicHash(userId.Value.ToString("D") + flagName);
                return (hash % 100) < flag.RolloutPercentage;
            }
        }

        return flag.RolloutPercentage == 0 && string.IsNullOrEmpty(flag.WhitelistUserIds);
    }

    private int GetDeterministicHash(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return Math.Abs(BitConverter.ToInt32(bytes, 0));
    }
}
