using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/categories")]
public class CategoryController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    private const string CACHE_KEY = "store:categories";
    private const int CACHE_DURATION = 600; // 10 min

    public CategoryController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var cached = await _cache.GetAsync(CACHE_KEY);
        if (!string.IsNullOrEmpty(cached))
            return Content(cached, "application/json");

        var categories = await _db.Select<FakaCategory>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();

        var result = categories.Select(c => new { id = c.Id, name = c.Name, sort_order = c.SortOrder });
        var json = JsonSerializer.Serialize(result);
        await _cache.SetAsync(CACHE_KEY, json, CACHE_DURATION);
        return Content(json, "application/json");
    }
}
