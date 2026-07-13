using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Nexustock.Modules.Putaway.Dtos;

public class PutawayProposalResponseDto
{
    public Guid LotId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string ItemName { get; set; } = null!;
    public string LotNo { get; set; } = null!;
    public decimal Qty { get; set; }
    public List<PutawayCandidateDto> Proposals { get; set; } = new();
    public List<ZoneLocationDto> ZoneLocations { get; set; } = new();
}

public class PutawayCandidateDto
{
    public Guid ProposalId { get; set; }
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = null!;
    public string ZoneCode { get; set; } = null!;
    public int Score { get; set; }
    public string Reason { get; set; } = null!;
}

public class ZoneLocationDto
{
    public Guid LocationId { get; set; }
    public string LocationCode { get; set; } = null!;
    public int X { get; set; }
    public int Y { get; set; }
    public int Z { get; set; }
    public string Status { get; set; } = null!; // PROPOSED, OCCUPIED, FREE
}

public class ConfirmPutawayRequestDto
{
    [Required]
    public Guid ProposalId { get; set; } // For idempotency & updating proposal status

    [Required]
    public Guid LotId { get; set; }

    [Required]
    public Guid FromLocationId { get; set; }

    [Required]
    public Guid SelectedLocationId { get; set; }

    [Required]
    [Range(0.0001, 9999999999)]
    public decimal Qty { get; set; }
}

public class RejectPutawayRequestDto
{
    [Required]
    public Guid ProposalId { get; set; }

    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    [MaxLength(250)]
    public string? Note { get; set; }
}
