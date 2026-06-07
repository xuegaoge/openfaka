using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.DTOs.Admin;
using OpenFaka.Core.Interfaces;
using FreeSql;
using OpenFaka.Core.Entities;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/products")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminProductController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    public AdminProductController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] long? categoryId = null, [FromQuery] bool? isEnabled = null)
    {
        var query = _db.Select<FakaProduct>()
            .Where(p => !p.IsDeleted)
            .WhereIf(categoryId.HasValue, p => p.CategoryId == categoryId.Value)
            .WhereIf(isEnabled.HasValue, p => p.IsEnabled == isEnabled.Value)
            .OrderByDescending(p => p.CreateTime)
            .Page(page + 1, count);

        var products = await query.ToListAsync();
        var total = await _db.Select<FakaProduct>()
            .Where(p => !p.IsDeleted)
            .WhereIf(categoryId.HasValue, p => p.CategoryId == categoryId.Value)
            .WhereIf(isEnabled.HasValue, p => p.IsEnabled == isEnabled.Value)
            .CountAsync();

        var result = products.Select(p => new ProductAdminDto
        {
            Id = p.Id,
            Title = p.Title,
            BasePrice = p.BasePrice,
            IsEnabled = p.IsEnabled,
            Sales = p.InitialSales,
            CreatedAt = p.CreateTime
        }).ToList();

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetProduct(long id)
    {
        var product = await _db.Select<FakaProduct>()
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstAsync();
        if (product == null) return NotFound();

        return Ok(product);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest request)
    {
        var product = new FakaProduct
        {
            Title = request.Title,
            Description = request.Description,
            BasePrice = request.BasePrice,
            CurrencyCode = request.CurrencyCode ?? "CNY",
            CategoryId = request.CategoryId,
            CoverUrl = request.CoverUrl,
            DeliveryType = request.DeliveryType,
            LowStockThreshold = request.LowStockThreshold,
            WholesaleEnabled = request.WholesaleEnabled,
            SpecEnabled = request.SpecEnabled,
            InitialSales = request.InitialSales,
            SortOrder = request.SortOrder,
            IsEnabled = true
        };

        product.CreateTime = DateTime.Now;
        var productId = await _db.Insert(product).ExecuteIdentityAsync();
        product.Id = productId;

        // 创建规格
        if (request.Specs?.Count > 0)
        {
            foreach (var spec in request.Specs)
            {
                await _db.Insert(new FakaProductSpec
                {
                    ProductId = product.Id,
                    Name = spec.Name,
                    Price = spec.Price,
                    SortOrder = spec.SortOrder
                }).ExecuteAffrowsAsync();
            }
        }

        await _cache.RemoveByPrefixAsync("store:products");
        return Ok(new { id = product.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProduct(long id, [FromBody] UpdateProductRequest request)
    {
        var product = await _db.Select<FakaProduct>()
            .Where(p => p.Id == id && !p.IsDeleted)
            .FirstAsync();
        if (product == null) return NotFound();

        await _db.Update<FakaProduct>()
            .Where(p => p.Id == id)
            .Set(p => p.Title, request.Title)
            .Set(p => p.Description, request.Description)
            .Set(p => p.BasePrice, request.BasePrice)
            .Set(p => p.CategoryId, request.CategoryId)
            .Set(p => p.CoverUrl, request.CoverUrl)
            .Set(p => p.DeliveryType, request.DeliveryType)
            .Set(p => p.LowStockThreshold, request.LowStockThreshold)
            .Set(p => p.WholesaleEnabled, request.WholesaleEnabled)
            .Set(p => p.SpecEnabled, request.SpecEnabled)
            .Set(p => p.SortOrder, request.SortOrder)
            .ExecuteAffrowsAsync();

        await _cache.RemoveByPrefixAsync("store:products");
        await _cache.RemoveAsync($"store:product:{id}");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        await _db.Update<FakaProduct>()
            .Where(p => p.Id == id)
            .Set(p => p.IsDeleted, true)
            .ExecuteAffrowsAsync();

        await _cache.RemoveByPrefixAsync("store:products");
        await _cache.RemoveAsync($"store:product:{id}");
        return Ok();
    }

    [HttpPut("{id}/enable")]
    public async Task<IActionResult> ToggleProduct(long id, [FromQuery] bool enable)
    {
        await _db.Update<FakaProduct>()
            .Where(p => p.Id == id && !p.IsDeleted)
            .Set(p => p.IsEnabled, enable)
            .ExecuteAffrowsAsync();

        await _cache.RemoveByPrefixAsync("store:products");
        await _cache.RemoveAsync($"store:product:{id}");
        return Ok();
    }

    // ========== Specs ==========

    [HttpGet("{productId}/specs")]
    public async Task<IActionResult> GetSpecs(long productId)
    {
        var specs = await _db.Select<FakaProductSpec>()
            .Where(s => s.ProductId == productId)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();
        return Ok(specs);
    }

    [HttpPost("{productId}/specs")]
    public async Task<IActionResult> AddSpec(long productId, [FromBody] CreateSpecRequest request)
    {
        var spec = new FakaProductSpec
        {
            ProductId = productId,
            Name = request.Name,
            Price = request.Price,
            IsVisible = request.IsVisible,
            SortOrder = request.SortOrder
        };
        await _db.Insert(spec).ExecuteAffrowsAsync();
        await _cache.RemoveAsync($"store:product:{productId}");
        return Ok(spec);
    }

    [HttpPut("{productId}/specs/{specId}")]
    public async Task<IActionResult> UpdateSpec(long productId, long specId, [FromBody] UpdateSpecRequest request)
    {
        await _db.Update<FakaProductSpec>()
            .Where(s => s.Id == specId && s.ProductId == productId)
            .Set(s => s.Name, request.Name)
            .Set(s => s.Price, request.Price)
            .Set(s => s.IsVisible, request.IsVisible)
            .Set(s => s.SortOrder, request.SortOrder)
            .ExecuteAffrowsAsync();
        await _cache.RemoveAsync($"store:product:{productId}");
        return Ok();
    }

    [HttpDelete("{productId}/specs/{specId}")]
    public async Task<IActionResult> DeleteSpec(long productId, long specId)
    {
        await _db.Delete<FakaProductSpec>()
            .Where(s => s.Id == specId && s.ProductId == productId)
            .ExecuteAffrowsAsync();
        await _cache.RemoveAsync($"store:product:{productId}");
        return Ok();
    }

    // ========== Wholesale Rules ==========

    [HttpGet("{productId}/wholesale-rules")]
    public async Task<IActionResult> GetWholesaleRules(long productId)
    {
        var rules = await _db.Select<FakaWholesaleRule>()
            .Where(r => r.ProductId == productId)
            .OrderBy(r => r.MinQuantity)
            .ToListAsync();
        return Ok(rules);
    }

    [HttpPost("{productId}/wholesale-rules")]
    public async Task<IActionResult> SetWholesaleRules(long productId, [FromBody] SetWholesaleRulesRequest request)
    {
        // 删除旧规则
        await _db.Delete<FakaWholesaleRule>()
            .Where(r => r.ProductId == productId)
            .ExecuteAffrowsAsync();

        // 插入新规则
        foreach (var rule in request.Rules)
        {
            await _db.Insert(new FakaWholesaleRule
            {
                ProductId = productId,
                SpecId = request.SpecId,
                MinQuantity = rule.MinQuantity,
                UnitPrice = rule.UnitPrice
            }).ExecuteAffrowsAsync();
        }

        await _cache.RemoveAsync($"store:product:{productId}");
        return Ok();
    }
}

public class CreateSpecRequest
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsVisible { get; set; } = true;
    public int SortOrder { get; set; }
}

public class UpdateSpecRequest
{
    public string Name { get; set; }
    public decimal Price { get; set; }
    public bool IsVisible { get; set; }
    public int SortOrder { get; set; }
}

public class SetWholesaleRulesRequest
{
    public long? SpecId { get; set; }
    public List<WholesaleRuleItem> Rules { get; set; }
}

public class WholesaleRuleItem
{
    public int MinQuantity { get; set; }
    public decimal UnitPrice { get; set; }
}
