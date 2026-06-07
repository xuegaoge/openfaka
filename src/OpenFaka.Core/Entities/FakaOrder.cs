using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;
using OpenFaka.Core.Enums;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_order")]
[Index("idx_faka_order_no", "OrderNo", true)]
[Index("idx_faka_order_idempotency", "IdempotencyKey", true)]
public class FakaOrder : FullAuditEntity<long, long>
{
    [Column(StringLength = 64, IsNullable = false)]
    public string OrderNo { get; set; }

    public long? UserId { get; set; }

    [Column(StringLength = 200)]
    public string Email { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal TotalAmount { get; set; }

    [Column(Precision = 10, Scale = 2)]
    public decimal ActualAmount { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public int OrderType { get; set; }

    [Column(StringLength = 50)]
    public string PaymentMethod { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    [Column(StringLength = 64)]
    public string IdempotencyKey { get; set; }

    public bool IsRiskFlagged { get; set; }

    [Column(StringLength = 50)]
    public string ClientIp { get; set; }

    [Column(StringLength = 100)]
    public string SessionToken { get; set; }

    [Column(StringLength = 500)]
    public string PaymentUrl { get; set; }

    [Column(StringLength = 500)]
    public string QrcodeUrl { get; set; }

    [Column(StringLength = 100)]
    public string EpayTradeNo { get; set; }

    [Column(StringLength = 200)]
    public string UsdtWalletAddress { get; set; }

    [Column(Precision = 18, Scale = 6)]
    public decimal? UsdtCryptoAmount { get; set; }

    [Column(StringLength = 100)]
    public string UsdtTradeId { get; set; }

    public UsdtChainType? UsdtChain { get; set; }

    [Column(StringLength = 200)]
    public string UsdtTxId { get; set; }

}
