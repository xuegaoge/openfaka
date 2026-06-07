using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_card_import_batch")]
public class FakaCardImportBatch : FullAuditEntity<long, long>
{
    public long ProductId { get; set; }

    public long? SpecId { get; set; }

    public long ImportedBy { get; set; }

    public int TotalCount { get; set; }

    public int SuccessCount { get; set; }

    public int FailCount { get; set; }

    public string FailDetail { get; set; }
}
