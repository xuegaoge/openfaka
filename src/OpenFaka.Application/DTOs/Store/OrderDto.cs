namespace OpenFaka.Application.DTOs.Store;

public class CreateOrderRequest
{
    public string Email { get; set; }
    public string PaymentMethod { get; set; }
    public string IdempotencyKey { get; set; }
    public List<OrderItemRequest> Items { get; set; }
}

public class OrderItemRequest
{
    public long ProductId { get; set; }
    public long? SpecId { get; set; }
    public int Quantity { get; set; }
}

public class OrderDto
{
    public long Id { get; set; }
    public string OrderNo { get; set; }
    public string Email { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public string Status { get; set; }
    public string OrderType { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string PaymentUrl { get; set; }
    public string QrcodeUrl { get; set; }
    public int PointsDeducted { get; set; }
    public decimal PointsDiscount { get; set; }
    public string UsdtTxId { get; set; }
    public decimal? UsdtCryptoAmount { get; set; }
    public string UsdtChain { get; set; }
    public string TxidReviewStatus { get; set; }
    public string TxidReviewReason { get; set; }
    public List<OrderItemDto> Items { get; set; }
    public List<OrderCardKeyDto> CardKeys { get; set; }
}

public class OrderItemDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public string ProductTitle { get; set; }
    public string SpecName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class OrderCardKeyDto
{
    public long Id { get; set; }
    public string Content { get; set; }
    public string ProductTitle { get; set; }
    public string SpecName { get; set; }
    public string Status { get; set; }
}
