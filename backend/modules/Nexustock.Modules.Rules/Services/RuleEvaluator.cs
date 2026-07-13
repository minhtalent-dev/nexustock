using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Rules.Contexts;
using Nexustock.Modules.Rules.Dtos;
using Nexustock.Modules.Rules.Entities;

namespace Nexustock.Modules.Rules.Services;

public interface IRuleEvaluator
{
    Task<EvaluateRuleResponse> EvaluateAsync(Guid tenantId, string ruleType, Dictionary<string, string> context, string actor = "System");
}

public class RuleEvaluator : IRuleEvaluator
{
    private readonly RulesDbContext _dbContext;

    public RuleEvaluator(RulesDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EvaluateRuleResponse> EvaluateAsync(Guid tenantId, string ruleType, Dictionary<string, string> context, string actor = "System")
    {
        var now = DateTime.UtcNow;

        // Fetch active rules sorted by priority descending
        var rules = await _dbContext.RuleSets
            .Where(r => r.TenantId == tenantId && 
                        r.Type == ruleType && 
                        r.IsActive && 
                        (r.ActiveFrom == null || r.ActiveFrom <= now) && 
                        (r.ActiveTo == null || r.ActiveTo >= now))
            .OrderByDescending(r => r.Priority)
            .ToListAsync();

        foreach (var rule in rules)
        {
            var conditions = await _dbContext.RuleConditions
                .Where(c => c.TenantId == tenantId && c.RuleSetId == rule.Id)
                .ToListAsync();

            bool allConditionsMatch = true;
            var detailsList = new List<string>();

            if (conditions.Count == 0)
            {
                allConditionsMatch = false; // A rule without conditions shouldn't auto-match, or should it? In our logic, conditions are required.
                detailsList.Add("Rule has no conditions defined.");
            }

            foreach (var cond in conditions)
            {
                // Find matching key in context (case-insensitive key comparison)
                var contextKey = context.Keys.FirstOrDefault(k => string.Equals(k, cond.Field, StringComparison.OrdinalIgnoreCase));
                
                if (contextKey == null)
                {
                    allConditionsMatch = false;
                    detailsList.Add($"Missing context field '{cond.Field}'.");
                    break;
                }

                var actualValue = context[contextKey] ?? string.Empty;
                bool conditionMatched = EvaluateCondition(cond.Operator, cond.Value, actualValue);

                if (!conditionMatched)
                {
                    allConditionsMatch = false;
                    detailsList.Add($"Condition failed: {cond.Field} ({actualValue}) {cond.Operator} {cond.Value}");
                    break;
                }
                else
                {
                    detailsList.Add($"Condition passed: {cond.Field} ({actualValue}) {cond.Operator} {cond.Value}");
                }
            }

            if (allConditionsMatch)
            {
                var action = await _dbContext.RuleActions
                    .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.RuleSetId == rule.Id);

                var actionType = action?.ActionType ?? "ALLOW";
                var actionParams = action?.ActionParameters;
                var details = $"Luật '{rule.Name}' ({rule.Code}) khớp thành công. Kết quả: {actionType}. Chi tiết: {string.Join("; ", detailsList)}";

                // Log execution
                var log = new RuleExecutionLog
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RuleSetId = rule.Id,
                    RuleTypeCode = ruleType,
                    InputContextJson = JsonSerializer.Serialize(context),
                    Matched = true,
                    ResultAction = actionType,
                    Details = details,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = actor
                };
                _dbContext.RuleExecutionLogs.Add(log);
                await _dbContext.SaveChangesAsync();

                return new EvaluateRuleResponse
                {
                    Matched = true,
                    RuleSetId = rule.Id,
                    RuleCode = rule.Code,
                    ActionType = actionType,
                    ActionParameters = actionParams,
                    Details = details
                };
            }
        }

        // No rules matched, return default ALLOW
        var defaultDetails = "Không khớp bất kỳ luật nào. Mặc định: ALLOW.";
        var defaultLog = new RuleExecutionLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RuleSetId = null,
            RuleTypeCode = ruleType,
            InputContextJson = JsonSerializer.Serialize(context),
            Matched = false,
            ResultAction = "ALLOW",
            Details = defaultDetails,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = actor
        };
        _dbContext.RuleExecutionLogs.Add(defaultLog);
        await _dbContext.SaveChangesAsync();

        return new EvaluateRuleResponse
        {
            Matched = false,
            ActionType = "ALLOW",
            Details = defaultDetails
        };
    }

    private bool EvaluateCondition(string op, string ruleVal, string actualVal)
    {
        switch (op.ToUpperInvariant())
        {
            case "EQUALS":
                return string.Equals(actualVal, ruleVal, StringComparison.OrdinalIgnoreCase);

            case "NOT_EQUALS":
                return !string.Equals(actualVal, ruleVal, StringComparison.OrdinalIgnoreCase);

            case "GREATER_THAN":
                if (decimal.TryParse(actualVal, out var actDecG) && decimal.TryParse(ruleVal, out var ruleDecG))
                {
                    return actDecG > ruleDecG;
                }
                return false;

            case "LESS_THAN":
                if (decimal.TryParse(actualVal, out var actDecL) && decimal.TryParse(ruleVal, out var ruleDecL))
                {
                    return actDecL < ruleDecL;
                }
                return false;

            case "IN":
                var inValues = ruleVal.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(v => v.Trim());
                return inValues.Any(v => string.Equals(v, actualVal, StringComparison.OrdinalIgnoreCase));

            case "NOT_IN":
                var notInValues = ruleVal.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(v => v.Trim());
                return !notInValues.Any(v => string.Equals(v, actualVal, StringComparison.OrdinalIgnoreCase));

            default:
                return false;
        }
    }
}
