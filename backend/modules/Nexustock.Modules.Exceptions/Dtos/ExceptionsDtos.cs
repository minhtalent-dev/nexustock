using System;

namespace Nexustock.Modules.Exceptions.Dtos;

public class CreateExceptionRequest
{
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = "MEDIUM";
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid? LocationId { get; set; }
    public string? LotNo { get; set; }
    public decimal Qty { get; set; }
    public string ReasonCode { get; set; } = null!;
    public string? Note { get; set; }
}

public class ResolveExceptionRequest
{
    public string Action { get; set; } = null!; // CORRECTIVE_TRANSACTION, CANCEL, etc.
    public string ReasonCode { get; set; } = null!;
    public string? Note { get; set; }
}

public class AssignExceptionRequest
{
    public string Owner { get; set; } = null!;
    public int SlaHours { get; set; }
}

public class ExceptionResponse
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string Severity { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string ReferenceType { get; set; } = null!;
    public Guid ReferenceId { get; set; }
    public Guid? LocationId { get; set; }
    public string? LotNo { get; set; }
    public decimal Qty { get; set; }
    public string ReasonCode { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = null!;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class ExceptionEventResponse
{
    public Guid Id { get; set; }
    public string Transition { get; set; } = null!;
    public string Actor { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
