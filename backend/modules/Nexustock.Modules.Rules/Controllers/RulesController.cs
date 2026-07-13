using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexustock.Modules.Identity.Services;
using Nexustock.Modules.Rules.Contexts;
using Nexustock.Modules.Rules.Dtos;
using Nexustock.Modules.Rules.Entities;
using Nexustock.Modules.Rules.Services;

namespace Nexustock.Modules.Rules.Controllers;

[Authorize]
[ApiController]
[Route("api/rules")]
public class RulesController : ControllerBase
{
    private readonly RulesDbContext _context;
    private readonly ITenantProvider _tenantProvider;
    private readonly IUserPermissionService _permissionService;
    private readonly IRuleEvaluator _evaluator;

    public RulesController(
        RulesDbContext context,
        ITenantProvider tenantProvider,
        IUserPermissionService permissionService,
        IRuleEvaluator evaluator)
    {
        _context = context;
        _tenantProvider = tenantProvider;
        _permissionService = permissionService;
        _evaluator = evaluator;
    }

    private Guid GetTenantId() => _tenantProvider.TenantId;

    private async Task<bool> HasPermissionAsync(string permissionName)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId)) return false;
        return await _permissionService.HasPermissionAsync(userId, permissionName);
    }

    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateRule([FromBody] EvaluateRuleRequest dto)
    {
        if (!await HasPermissionAsync("rule_engine_foundation.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        var response = await _evaluator.EvaluateAsync(tenantId, dto.RuleType, dto.Context, username);
        return Ok(response);
    }

    [HttpGet]
    public async Task<IActionResult> GetRules([FromQuery] string? type)
    {
        if (!await HasPermissionAsync("rule_engine_foundation.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.RuleSets.Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(r => r.Type == type);
        }

        var rules = await query.OrderByDescending(r => r.Priority).ToListAsync();
        var response = new List<RuleResponse>();

        foreach (var rule in rules)
        {
            var conditions = await _context.RuleConditions
                .Where(c => c.TenantId == tenantId && c.RuleSetId == rule.Id)
                .Select(c => new ConditionDto
                {
                    Field = c.Field,
                    Operator = c.Operator,
                    Value = c.Value
                })
                .ToListAsync();

            var actionEntity = await _context.RuleActions
                .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.RuleSetId == rule.Id);

            var actionDto = new ActionDto
            {
                ActionType = actionEntity?.ActionType ?? "ALLOW",
                ActionParameters = actionEntity?.ActionParameters
            };

            response.Add(new RuleResponse
            {
                Id = rule.Id,
                Code = rule.Code,
                Name = rule.Name,
                Type = rule.Type,
                Priority = rule.Priority,
                IsActive = rule.IsActive,
                ActiveFrom = rule.ActiveFrom,
                ActiveTo = rule.ActiveTo,
                Conditions = conditions,
                Action = actionDto,
                CreatedAt = rule.CreatedAt,
                CreatedBy = rule.CreatedBy
            });
        }

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] CreateRuleRequest dto)
    {
        if (!await HasPermissionAsync("rule_engine_foundation.create"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var username = User.Identity?.Name ?? "System";

        // Check unique code
        var codeExists = await _context.RuleSets
            .AnyAsync(r => r.TenantId == tenantId && r.Code == dto.Code);
        if (codeExists)
        {
            return BadRequest($"Mã luật '{dto.Code}' đã tồn tại.");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var ruleSet = new RuleSet
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = dto.Code,
                Name = dto.Name,
                Type = dto.Type,
                Priority = dto.Priority,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.RuleSets.Add(ruleSet);

            foreach (var condDto in dto.Conditions)
            {
                var condition = new RuleCondition
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    RuleSetId = ruleSet.Id,
                    Field = condDto.Field,
                    Operator = condDto.Operator,
                    Value = condDto.Value,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = username
                };
                _context.RuleConditions.Add(condition);
            }

            var action = new RuleAction
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                RuleSetId = ruleSet.Id,
                ActionType = dto.Action.ActionType,
                ActionParameters = dto.Action.ActionParameters,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = username
            };
            _context.RuleActions.Add(action);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return CreatedAtAction(nameof(GetRules), new { type = ruleSet.Type }, new RuleResponse
            {
                Id = ruleSet.Id,
                Code = ruleSet.Code,
                Name = ruleSet.Name,
                Type = ruleSet.Type,
                Priority = ruleSet.Priority,
                IsActive = ruleSet.IsActive,
                Conditions = dto.Conditions,
                Action = dto.Action,
                CreatedAt = ruleSet.CreatedAt,
                CreatedBy = ruleSet.CreatedBy
            });
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] string? ruleType,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!await HasPermissionAsync("rule_engine_foundation.read"))
        {
            return Forbid();
        }

        var tenantId = GetTenantId();
        var query = _context.RuleExecutionLogs.Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(ruleType))
        {
            query = query.Where(l => l.RuleTypeCode == ruleType);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new RuleExecutionLogResponse
            {
                Id = l.Id,
                RuleSetId = l.RuleSetId,
                RuleTypeCode = l.RuleTypeCode,
                InputContextJson = l.InputContextJson,
                Matched = l.Matched,
                ResultAction = l.ResultAction,
                Details = l.Details,
                CreatedAt = l.CreatedAt
            })
            .ToListAsync();

        return Ok(new { items, totalCount });
    }
}
