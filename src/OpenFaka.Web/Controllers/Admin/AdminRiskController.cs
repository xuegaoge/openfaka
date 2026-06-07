using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/risk")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminRiskController : ControllerBase
{
    private readonly IFreeSql _db;

    public AdminRiskController(IFreeSql db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetConfig()
    {
        // 返回风控配置（使用 lin_settings 存储）
        var settings = await _db.Select<LinCms.Entities.Settings.LinSetting>()
            .Where(s => s.Name.StartsWith("risk_"))
            .ToListAsync();

        var result = settings.ToDictionary(s => s.Name, s => s.Value);

        return Ok(new
        {
            maxOrdersPerIp = result.GetValueOrDefault("risk_max_orders_per_ip", "10"),
            maxOrdersPerEmail = result.GetValueOrDefault("risk_max_orders_per_email", "20"),
            orderCooldownMinutes = result.GetValueOrDefault("risk_order_cooldown_minutes", "5"),
            enableDeviceFingerprint = result.GetValueOrDefault("risk_enable_device_fingerprint", "true"),
            enableCaptcha = result.GetValueOrDefault("risk_enable_captcha", "true")
        });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateConfig([FromBody] RiskConfigRequest request)
    {
        async Task SetSetting(string name, string value)
        {
            var existing = await _db.Select<LinCms.Entities.Settings.LinSetting>()
                .Where(s => s.Name == name)
                .FirstAsync();

            if (existing != null)
            {
                await _db.Update<LinCms.Entities.Settings.LinSetting>()
                    .Where(s => s.Id == existing.Id)
                    .Set(s => s.Value, value)
                    .ExecuteAffrowsAsync();
            }
            else
            {
                await _db.Insert(new LinCms.Entities.Settings.LinSetting
                {
                    Name = name,
                    Value = value
                }).ExecuteAffrowsAsync();
            }
        }

        await SetSetting("risk_max_orders_per_ip", request.MaxOrdersPerIp);
        await SetSetting("risk_max_orders_per_email", request.MaxOrdersPerEmail);
        await SetSetting("risk_order_cooldown_minutes", request.OrderCooldownMinutes);
        await SetSetting("risk_enable_device_fingerprint", request.EnableDeviceFingerprint);
        await SetSetting("risk_enable_captcha", request.EnableCaptcha);

        return Ok();
    }

    [HttpGet("flagged-orders")]
    public async Task<IActionResult> GetFlaggedOrders([FromQuery] int page = 0, [FromQuery] int count = 20)
    {
        var query = _db.Select<FakaOrder>()
            .Where(o => o.IsRiskFlagged)
            .OrderByDescending(o => o.CreateTime)
            .Page(page + 1, count);

        var orders = await query.ToListAsync();
        var total = await _db.Select<FakaOrder>()
            .Where(o => o.IsRiskFlagged)
            .CountAsync();

        var result = orders.Select(o => new
        {
            id = o.Id,
            orderNo = o.OrderNo,
            email = o.Email,
            totalAmount = o.TotalAmount,
            status = o.Status.ToString().ToLower(),
            clientIp = o.ClientIp,
            createTime = o.CreateTime
        });

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }
}

public class RiskConfigRequest
{
    public string MaxOrdersPerIp { get; set; }
    public string MaxOrdersPerEmail { get; set; }
    public string OrderCooldownMinutes { get; set; }
    public string EnableDeviceFingerprint { get; set; }
    public string EnableCaptcha { get; set; }
}
