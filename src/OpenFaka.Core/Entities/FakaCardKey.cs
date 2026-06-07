using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;
using OpenFaka.Core.Enums;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_card_key")]
[Index("idx_faka_card_key_product_spec", "ProductId,SpecId")]
[Index("idx_faka_card_key_order_item", "OrderItemId")]
public class FakaCardKey : FullAuditEntity<long, long>
{
    public long ProductId { get; set; }

    public long? SpecId { get; set; }

    [Column(StringLength = 2000)]
    public string Content { get; set; }

    public CardKeyStatus Status { get; set; } = CardKeyStatus.Available;

    public long? OrderId { get; set; }

    public long? OrderItemId { get; set; }

    public long? ImportBatchId { get; set; }

    /// <summary>
    /// 乐观锁版本号
    /// </summary>
    public long Version { get; set; }

    public DateTime? SoldAt { get; set; }

}
