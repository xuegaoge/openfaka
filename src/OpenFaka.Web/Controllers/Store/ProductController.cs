using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.DTOs.Store;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/products")]
public class ProductController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    private const int CACHE_DURATION_LIST = 60; // 1 min for list (stock changes)
    private const int CACHE_DURATION_DETAIL = 30; // 30s for detail

    public ProductController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] long? categoryId = null)
    {
        var cacheKey = $"store:products:p{page}:c{count}:cat{categoryId}";
        var cached = await _cache.GetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
            return Content(cached, "application/json");

        var query = _db.Select<FakaProduct>()
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .WhereIf(categoryId.HasValue, p => p.CategoryId == categoryId.Value)
            .OrderBy(p => p.SortOrder)
            .Page(page + 1, count);

        var products = await query.ToListAsync();
        var total = await _db.Select<FakaProduct>()
            .Where(p => p.IsEnabled && !p.IsDeleted)
            .WhereIf(categoryId.HasValue, p => p.CategoryId == categoryId.Value)
            .CountAsync();

        var productIds = products.Select(p => p.Id).ToList();
        var allSpecs = productIds.Count > 0
            ? await _db.Select<FakaProductSpec>()
                .Where(s => productIds.Contains(s.ProductId) && s.IsVisible)
                .OrderBy(s => s.SortOrder)
                .ToListAsync()
            : new List<FakaProductSpec>();
        var allStocks = productIds.Count > 0
            ? await _db.Select<FakaCardKey>()
                .Where(c => productIds.Contains(c.ProductId) && c.Status == Core.Enums.CardKeyStatus.Available)
                .ToListAsync()
            : new List<FakaCardKey>();

        var result = new List<ProductListDto>();
        foreach (var p in products)
        {
            var specs = allSpecs.Where(s => s.ProductId == p.Id).ToList();
            var stockCount = allStocks.Count(c => c.ProductId == p.Id);

            result.Add(new ProductListDto
            {
                Id = p.Id,
                Title = p.Title,
                Description = p.Description,
                CoverUrl = p.CoverUrl,
                BasePrice = p.BasePrice,
                Currency = p.CurrencyCode ?? "CNY",
                CategoryId = p.CategoryId,
                StockAvailable = stockCount,
                HasSpecs = p.SpecEnabled,
                DeliveryType = p.DeliveryType.ToString(),
                SalesCount = 0,
                InitialSales = p.InitialSales,
                IsEnabled = p.IsEnabled,
                SortOrder = p.SortOrder,
                CreatedAt = p.CreateTime,
                WholesaleEnabled = p.WholesaleEnabled,
                SpecEnabled = p.SpecEnabled,
                Specs = specs.Select(s => new ProductSpecDto
                {
                    Id = s.Id,
                    Name = s.Name,
                    Price = s.Price
                }).ToList()
            });
        }

        var json = JsonSerializer.Serialize(new { list = result, pagination = new { page, page_size = count, total } });
        await _cache.SetAsync(cacheKey, json, CACHE_DURATION_LIST);
        return Content(json, "application/json");
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(long id)
    {
        var cacheKey = $"store:product:{id}";
        var cached = await _cache.GetAsync(cacheKey);
        if (!string.IsNullOrEmpty(cached))
            return Content(cached, "application/json");

        var product = await _db.Select<FakaProduct>()
            .Where(p => p.Id == id && p.IsEnabled && !p.IsDeleted)
            .FirstAsync();
        if (product == null)
            return NotFound();

        var specs = await _db.Select<FakaProductSpec>()
            .Where(s => s.ProductId == product.Id && s.IsVisible)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        var wholesaleRules = await _db.Select<FakaWholesaleRule>()
            .Where(r => r.ProductId == product.Id)
            .OrderBy(r => r.MinQuantity)
            .ToListAsync();

        var allStocks = await _db.Select<FakaCardKey>()
            .Where(c => c.ProductId == product.Id && c.Status == Core.Enums.CardKeyStatus.Available)
            .ToListAsync();

        var stockCount = allStocks.Count;

        var category = await _db.Select<FakaCategory>()
            .Where(c => c.Id == product.CategoryId)
            .FirstAsync();

        var specDtos = new List<ProductSpecDto>();
        foreach (var s in specs)
        {
            var specStock = allStocks.Count(c => c.SpecId == s.Id);
            specDtos.Add(new ProductSpecDto
            {
                Id = s.Id,
                Name = s.Name,
                Price = s.Price,
                StockAvailable = specStock,
                IsVisible = s.IsVisible,
                SortOrder = s.SortOrder
            });
        }

        var dto = new ProductDetailDto
        {
            Id = product.Id,
            Title = product.Title,
            Description = product.Description,
            CoverUrl = product.CoverUrl,
            BasePrice = product.BasePrice,
            Currency = product.CurrencyCode ?? "CNY",
            CategoryId = product.CategoryId,
            StockAvailable = stockCount,
            HasSpecs = product.SpecEnabled,
            DeliveryType = product.DeliveryType.ToString(),
            SalesCount = 0,
            InitialSales = product.InitialSales,
            IsEnabled = product.IsEnabled,
            SortOrder = product.SortOrder,
            CreatedAt = product.CreateTime,
            WholesaleEnabled = product.WholesaleEnabled,
            SpecEnabled = product.SpecEnabled,
            Specs = specDtos,
            DetailMd = product.Description,
            WholesaleRules = wholesaleRules.Select(r => new WholesaleRuleDto
            {
                MinQuantity = r.MinQuantity,
                UnitPrice = r.UnitPrice
            }).ToList(),
            LowStockThreshold = product.LowStockThreshold,
            CategoryName = category?.Name,
            UpdatedAt = product.UpdateTime
        };

        var json = JsonSerializer.Serialize(dto);
        await _cache.SetAsync(cacheKey, json, CACHE_DURATION_DETAIL);
        return Content(json, "application/json");
    }
}
