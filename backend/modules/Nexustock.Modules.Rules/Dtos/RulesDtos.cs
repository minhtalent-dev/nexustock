using System;
using System.Collections.Generic;

namespace Nexustock.Modules.Rules.Dtos;

public class CreateRuleRequest
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Priority { get; set; }
    public List<ConditionDto> Conditions { get; set; } = new();
    public ActionDto Action { get; set; } = null!;
}

public class ConditionDto
{
    public string Field { get; set; } = null!;
    public string Operator { get; set; } = null!;
    public string Value { get; set; } = null!;
}

public class ActionDto
{
    public string ActionType { get; set; } = null!;
    public string? ActionParameters { get; set; }
}

public class EvaluateRuleRequest
{
    public string RuleType { get; set; } = null!; // e.g. PUTAWAY
    public Dictionary<string, string> Context { get; set; } = new(); // e.g. {"productGroup": "CHEMICAL"}
}

public class EvaluateRuleResponse
{
    public bool Matched { get; set; }
    public Guid? RuleSetId { get; set; }
    public string? RuleCode { get; set; }
    public string ActionType { get; set; } = "ALLOW"; // Default is ALLOW
    public string? ActionParameters { get; set; }
    public string? Details { get; set; }
}

public class RuleResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int Priority { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ActiveFrom { get; set; }
    public DateTime? ActiveTo { get; set; }
    public List<ConditionDto> Conditions { get; set; } = new();
    public ActionDto Action { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
}

public class RuleExecutionLogResponse
{
    public Guid Id { get; set; }
    public Guid? RuleSetId { get; set; }
    public string RuleTypeCode { get; set; } = null!;
    public string InputContextJson { get; set; } = null!;
    public bool Matched { get; set; }
    public string ResultAction { get; set; } = null!;
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}
