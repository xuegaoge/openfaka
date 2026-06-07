namespace OpenFaka.Application.DTOs.Admin;

public class OrderAdminDto
{
    public string Id { get; set; }
    public string OrderNo { get; set; }
    public string Email { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal ActualAmount { get; set; }
    public int OrderType { get; set; }
    public string Status { get; set; }
    public string PaymentMethod { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public int ItemCount { get; set; }
    public List<OrderItemAdminDto> Items { get; set; }
    public string Username { get; set; }
    public bool IsRiskFlagged { get; set; }
}

public class OrderDetailAdminDto : OrderAdminDto
{
    public List<OrderItemAdminDto> Items { get; set; }
    public string ClientIp { get; set; }
    public string EpayTradeNo { get; set; }
}

public class OrderItemAdminDto
{
    public string ProductTitle { get; set; }
    public string SpecName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
