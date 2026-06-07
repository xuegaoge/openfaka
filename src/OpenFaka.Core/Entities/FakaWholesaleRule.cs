using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_wholesale_rule")]
public class FakaWholesaleRule : FullAuditEntity<long, long>
{
    public long ProductId { get; set; }

    public long? SpecId { get; set; }

    public int MinQuantity { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal UnitPrice { get; set; }
}
