namespace OpenFaka.Application.DTOs.Store;

public class CartItemDto
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long? SpecId { get; set; }
    public string ProductTitle { get; set; }
    public string SpecName { get; set; }
    public string CoverUrl { get; set; }
    public string Currency { get; set; }
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public int StockAvailable { get; set; }
}

public class AddCartItemRequest
{
    public long ProductId { get; set; }
    public long? SpecId { get; set; }
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    public int Quantity { get; set; }
}
