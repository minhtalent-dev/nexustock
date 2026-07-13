using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.Rules.Entities;

[Table("rule_sets")]
public class RuleSet
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("code")]
    public string Code { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    [Column("type")]
    public string Type { get; set; } = null!; // PUTAWAY, ALLOCATION, etc.

    [Column("priority")]
    public int Priority { get; set; } = 0;

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("active_from")]
    public DateTime? ActiveFrom { get; set; }

    [Column("active_to")]
    public DateTime? ActiveTo { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [MaxLength(100)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Timestamp]
    [Column("xmin", TypeName = "xid")]
    public uint RowVersion { get; set; }
}

[Table("rule_conditions")]
public class RuleCondition
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("rule_set_id")]
    public Guid RuleSetId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("field")]
    public string Field { get; set; } = null!; // productGroup, locationZone, etc.

    [Required]
    [MaxLength(20)]
    [Column("operator")]
    public string Operator { get; set; } = null!; // EQUALS, NOT_EQUALS, GREATER_THAN, LESS_THAN, IN, NOT_IN

    [Required]
    [MaxLength(200)]
    [Column("value")]
    public string Value { get; set; } = null!; // Comparing value as string

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}

[Table("rule_actions")]
public class RuleAction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("rule_set_id")]
    public Guid RuleSetId { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("action_type")]
    public string ActionType { get; set; } = null!; // ALLOW, WARN, BLOCK, PROPOSE_LOCATION

    [MaxLength(1000)]
    [Column("action_parameters")]
    public string? ActionParameters { get; set; } // JSON format parameters

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}

[Table("rule_execution_logs")]
public class RuleExecutionLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("rule_set_id")]
    public Guid? RuleSetId { get; set; } // Can be null if no rule matched

    [Required]
    [MaxLength(50)]
    [Column("rule_type_code")]
    public string RuleTypeCode { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    [Column("input_context_json")]
    public string InputContextJson { get; set; } = null!;

    [Column("matched")]
    public bool Matched { get; set; }

    [Required]
    [MaxLength(50)]
    [Column("result_action")]
    public string ResultAction { get; set; } = "ALLOW";

    [MaxLength(2000)]
    [Column("details")]
    public string? Details { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Required]
    [MaxLength(100)]
    [Column("created_by")]
    public string CreatedBy { get; set; } = null!;
}
