using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nexustock.Modules.MasterData.Entities;

[Table("import_batch_rows")]
public class ImportBatchRow
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("batch_id")]
    public Guid BatchId { get; set; }

    [Column("row_index")]
    public int RowIndex { get; set; }

    [Column("raw_data", TypeName = "jsonb")]
    public string? RawData { get; set; }

    [Column("is_valid")]
    public bool IsValid { get; set; } = true;

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [ForeignKey("BatchId")]
    public virtual ImportBatch? Batch { get; set; }
}
