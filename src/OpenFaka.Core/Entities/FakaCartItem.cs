using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_cart_item")]
public class FakaCartItem : FullAuditEntity<long, long>
{
    public long? UserId { get; set; }

    [Column(StringLength = 100)]
    public string SessionToken { get; set; }

    public long ProductId { get; set; }

    public long? SpecId { get; set; }

    public int Quantity { get; set; }
}
