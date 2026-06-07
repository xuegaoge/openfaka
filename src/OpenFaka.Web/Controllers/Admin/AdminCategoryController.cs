using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/categories")]
[Authorize(Roles = LinCms.Entities.LinGroup.Admin)]
public class AdminCategoryController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    public AdminCategoryController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await _db.Select<FakaCategory>()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.SortOrder)
            .ToListAsync();
        return Ok(categories.Select(c => new { id = c.Id, name = c.Name, sort_order = c.SortOrder }));
    }

    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        var category = new FakaCategory
        {
            Name = request.Name,
            SortOrder = request.SortOrder
        };
        category.Id = await _db.Insert(category).ExecuteIdentityAsync();
        await _cache.RemoveAsync("store:categories");
        await _cache.RemoveByPrefixAsync("store:products");
        return Ok(new { id = category.Id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(long id, [FromBody] UpdateCategoryRequest request)
    {
        await _db.Update<FakaCategory>()
            .Where(c => c.Id == id)
            .Set(c => c.Name, request.Name)
            .Set(c => c.SortOrder, request.SortOrder)
            .ExecuteAffrowsAsync();
        await _cache.RemoveAsync("store:categories");
        await _cache.RemoveByPrefixAsync("store:products");
        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(long id)
    {
        await _db.Update<FakaCategory>()
            .Where(c => c.Id == id)
            .Set(c => c.IsDeleted, true)
            .ExecuteAffrowsAsync();
        await _cache.RemoveAsync("store:categories");
        await _cache.RemoveByPrefixAsync("store:products");
        return Ok();
    }
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}

public class UpdateCategoryRequest
{
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
}
