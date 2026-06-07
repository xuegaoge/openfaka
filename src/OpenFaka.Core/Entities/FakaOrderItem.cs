using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_order_item")]
public class FakaOrderItem : FullAuditEntity<long, long>
{
    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public long? SpecId { get; set; }

    [Column(StringLength = 200)]
    public string ProductTitle { get; set; }

    [Column(StringLength = 100)]
    public string SpecName { get; set; }

    public int Quantity { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal UnitPrice { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal Subtotal { get; set; }
}
