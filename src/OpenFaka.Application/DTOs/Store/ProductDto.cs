namespace OpenFaka.Application.DTOs.Store;

public class ProductListDto
{
    public long Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string CoverUrl { get; set; }
    public decimal BasePrice { get; set; }
    public string Currency { get; set; }
    public long? CategoryId { get; set; }
    public int StockAvailable { get; set; }
    public bool HasSpecs { get; set; }
    public string DeliveryType { get; set; }
    public int SalesCount { get; set; }
    public int InitialSales { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool WholesaleEnabled { get; set; }
    public bool SpecEnabled { get; set; }
    public List<ProductSpecDto> Specs { get; set; }
}

public class ProductSpecDto
{
    public long Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public int StockAvailable { get; set; }
    public bool IsVisible { get; set; }
    public int SortOrder { get; set; }
}

public class ProductDetailDto : ProductListDto
{
    public string DetailMd { get; set; }
    public List<WholesaleRuleDto> WholesaleRules { get; set; }
    public int LowStockThreshold { get; set; }
    public string CategoryName { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class WholesaleRuleDto
{
    public int MinQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}
