namespace OpenFaka.Application.DTOs.Admin;

public class CreateProductRequest
{
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal BasePrice { get; set; }
    public string CurrencyCode { get; set; } = "CNY";
    public long? CategoryId { get; set; }
    public string CoverUrl { get; set; }
    public int DeliveryType { get; set; }
    public int LowStockThreshold { get; set; } = 10;
    public bool WholesaleEnabled { get; set; }
    public bool SpecEnabled { get; set; }
    public int InitialSales { get; set; }
    public int SortOrder { get; set; }
    public List<CreateProductSpecRequest> Specs { get; set; }
}

public class CreateProductSpecRequest
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
}

public class UpdateProductRequest
{
    public string Title { get; set; }
    public string Description { get; set; }
    public decimal BasePrice { get; set; }
    public long? CategoryId { get; set; }
    public string CoverUrl { get; set; }
    public int DeliveryType { get; set; }
    public int LowStockThreshold { get; set; }
    public bool WholesaleEnabled { get; set; }
    public bool SpecEnabled { get; set; }
    public int SortOrder { get; set; }
}

public class ProductAdminDto
{
    public long Id { get; set; }
    public string Title { get; set; }
    public decimal BasePrice { get; set; }
    public string CategoryName { get; set; }
    public bool IsEnabled { get; set; }
    public int StockCount { get; set; }
    public int Sales { get; set; }
    public DateTime CreatedAt { get; set; }
}
