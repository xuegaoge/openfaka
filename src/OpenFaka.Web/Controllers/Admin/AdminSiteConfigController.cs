using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using LinCms.Entities;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/site-config")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminSiteConfigController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    public AdminSiteConfigController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        var settings = await _db.Select<LinCms.Entities.Settings.LinSetting>().ToListAsync();
        return Ok(settings.Select(s => new { config_key = s.Name, config_value = s.Value }));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] List<ConfigKeyValuePair> configs)
    {
        foreach (var config in configs)
        {
            var row = await _db.Update<LinCms.Entities.Settings.LinSetting>()
                .Where(s => s.Name == config.Key)
                .Set(s => s.Value, config.Value ?? "")
                .ExecuteAffrowsAsync();

            if (row == 0)
            {
                await _db.Insert(new LinCms.Entities.Settings.LinSetting
                {
                    Name = config.Key,
                    Value = config.Value ?? ""
                }).ExecuteAffrowsAsync();
            }
        }

        // 清除站点配置缓存
        await _cache.RemoveAsync("store:site_config");

        return Ok();
    }

    [HttpPost("maintenance")]
    public async Task<IActionResult> ToggleMaintenance([FromBody] ToggleMaintenanceRequest request)
    {
        var row = await _db.Update<LinCms.Entities.Settings.LinSetting>()
            .Where(s => s.Name == "site_maintenance")
            .Set(s => s.Value, request.Enabled ? "true" : "false")
            .ExecuteAffrowsAsync();

        await _cache.RemoveAsync("store:site_config");
        return Ok(new { maintenance_enabled = request.Enabled });
    }

    public class ToggleMaintenanceRequest
    {
        public bool Enabled { get; set; }
    }

    public class ConfigKeyValuePair
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
