using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_product")]
public class FakaProduct : FullAuditEntity<long, long>
{
    [Column(StringLength = 200)]
    public string Title { get; set; }

    public string Description { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal BasePrice { get; set; }

    [Column(StringLength = 10)]
    public string CurrencyCode { get; set; } = "CNY";

    public long? CategoryId { get; set; }

    [Column(StringLength = 500)]
    public string CoverUrl { get; set; }

    public int DeliveryType { get; set; }

    public int LowStockThreshold { get; set; } = 10;

    public bool WholesaleEnabled { get; set; }

    public bool SpecEnabled { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int InitialSales { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }
}
