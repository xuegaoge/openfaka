using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using LinCms.Entities;
using OpenFaka.Core.Entities.Cms;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/users")]
[Authorize(Roles = LinCms.Entities.LinGroup.Admin)]
public class AdminUserController : ControllerBase
{
    private readonly IFreeSql _db;

    public AdminUserController(IFreeSql db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] string keyword = null)
    {
        var query = _db.Select<OpenFaka.Core.Entities.Cms.LinUser>()
            .WhereIf(!string.IsNullOrEmpty(keyword), u =>
                u.Username.Contains(keyword) || u.Email.Contains(keyword))
            .OrderByDescending(u => u.CreateTime)
            .Page(page + 1, count);

        var users = await query.ToListAsync();
        var total = await _db.Select<OpenFaka.Core.Entities.Cms.LinUser>()
            .WhereIf(!string.IsNullOrEmpty(keyword), u =>
                u.Username.Contains(keyword) || u.Email.Contains(keyword))
            .CountAsync();

        var result = users.Select(u => new
        {
            id = u.Id,
            username = u.Username,
            nickname = u.Nickname,
            email = u.Email,
            active = u.Active,
            lastLoginTime = u.LastLoginTime,
            createTime = u.CreateTime
        });

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }

    [HttpPut("{id}/toggle")]
    public async Task<IActionResult> ToggleUser(long id, [FromBody] ToggleUserRequest request)
    {
        var user = await _db.Select<OpenFaka.Core.Entities.Cms.LinUser>()
            .Where(u => u.Id == id)
            .FirstAsync();
        if (user == null) return NotFound();

        var newStatus = request.IsActive ? 1 : 2;

        await _db.Update<OpenFaka.Core.Entities.Cms.LinUser>()
            .Where(u => u.Id == id)
            .Set(u => u.Active, newStatus)
            .ExecuteAffrowsAsync();

        return Ok();
    }
}

public class ToggleUserRequest
{
    public bool IsActive { get; set; }
}
