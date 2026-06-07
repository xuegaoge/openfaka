using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_product_spec")]
public class FakaProductSpec : FullAuditEntity<long, long>
{
    public long ProductId { get; set; }

    [Column(StringLength = 100)]
    public string Name { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal Price { get; set; }

    public bool IsVisible { get; set; } = true;

    public int SortOrder { get; set; }
}
