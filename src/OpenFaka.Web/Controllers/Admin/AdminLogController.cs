using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/logs")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminLogController : ControllerBase
{
    private readonly IFreeSql _db;

    public AdminLogController(IFreeSql db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] string keyword = null)
    {
        var query = _db.Select<LinCms.Entities.LinLog>()
            .WhereIf(!string.IsNullOrEmpty(keyword), l =>
                l.Message.Contains(keyword) || l.Method.Contains(keyword))
            .OrderByDescending(l => l.CreateTime)
            .Page(page + 1, count);

        var logs = await query.ToListAsync();
        var total = await _db.Select<LinCms.Entities.LinLog>()
            .WhereIf(!string.IsNullOrEmpty(keyword), l =>
                l.Message.Contains(keyword) || l.Method.Contains(keyword))
            .CountAsync();

        var result = logs.Select(l => new
        {
            id = l.Id,
            message = l.Message,
            method = l.Method,
            path = l.Path,
            username = l.Username,
            statusCode = l.StatusCode,
            createTime = l.CreateTime
        });

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }
}
