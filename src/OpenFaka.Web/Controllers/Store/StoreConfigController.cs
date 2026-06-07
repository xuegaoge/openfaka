using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store")]
public class StoreConfigController : ControllerBase
{
    private readonly IFreeSql _db;
    private readonly ICacheService _cache;

    private const string CACHE_KEY_SITE = "store:site_config";
    private const string CACHE_KEY_CHANNELS = "store:payment_channels";
    private const int CACHE_DURATION_SITE = 300;     // 5 min
    private const int CACHE_DURATION_CHANNELS = 600; // 10 min

    public StoreConfigController(IFreeSql db, ICacheService cache)
    {
        _db = db;
        _cache = cache;
    }

    [HttpGet("config")]
    public async Task<IActionResult> GetSiteConfig()
    {
        var json = await _cache.GetOrSetAsync(CACHE_KEY_SITE, async () =>
        {
            var settings = await _db.Select<LinCms.Entities.Settings.LinSetting>()
                .Where(s => s.Name.StartsWith("site_"))
                .ToListAsync();

            var result = settings.ToDictionary(s => s.Name, s => s.Value);

            var data = new
            {
                site_name = result.GetValueOrDefault("site_name", "OpenFaka"),
                site_slogan = result.GetValueOrDefault("site_slogan", ""),
                site_description = result.GetValueOrDefault("site_description", ""),
                logo_url = result.GetValueOrDefault("site_logo", ""),
                favicon_url = result.GetValueOrDefault("site_favicon", ""),
                announcement_enabled = result.GetValueOrDefault("site_announcement_enabled", "false") == "true",
                announcement = result.GetValueOrDefault("site_announcement", ""),
                popup_enabled = result.GetValueOrDefault("site_popup_enabled", "false") == "true",
                popup_content = result.GetValueOrDefault("site_popup_content", ""),
                contact_email = result.GetValueOrDefault("site_contact_email", ""),
                contact_telegram = result.GetValueOrDefault("site_contact_telegram", ""),
                contact_telegram_group = result.GetValueOrDefault("site_contact_telegram_group", ""),
                points_enabled = result.GetValueOrDefault("site_points_enabled", "false") == "true",
                points_rate = decimal.TryParse(result.GetValueOrDefault("site_points_rate", "1"), out var rate) ? rate : 1,
                maintenance_enabled = result.GetValueOrDefault("site_maintenance", "false") == "true",
                maintenance_message = result.GetValueOrDefault("site_maintenance_message", ""),
                footer_text = result.GetValueOrDefault("site_footer_text", ""),
                github_url = result.GetValueOrDefault("site_github_url", ""),
                custom_css = result.GetValueOrDefault("site_custom_css", "")
            };

            return JsonSerializer.Serialize(data);
        }, CACHE_DURATION_SITE);

        return Content(json ?? "{}", "application/json");
    }

    [HttpGet("payment-channels")]
    public async Task<IActionResult> GetPaymentChannels()
    {
        var json = await _cache.GetOrSetAsync(CACHE_KEY_CHANNELS, async () =>
        {
            var channels = await _db.Select<FakaPaymentChannel>()
                .Where(c => c.IsEnabled && !c.IsDeleted)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();

            var result = channels.Select(c => new
            {
                id = c.Id,
                channel_code = c.ChannelCode,
                channel_name = c.ChannelName,
                provider_type = c.ProviderType.ToString().ToLower(),
                is_enabled = c.IsEnabled,
                sort_order = c.SortOrder,
                created_at = c.CreateTime
            });

            return JsonSerializer.Serialize(result);
        }, CACHE_DURATION_CHANNELS);

        return Content(json ?? "[]", "application/json");
    }

    [HttpGet("currencies")]
    public IActionResult GetCurrencies()
    {
        return Ok(new[]
        {
            new { code = "CNY", name = "人民币", symbol = "¥" },
            new { code = "USD", name = "美元", symbol = "$" },
            new { code = "USDT", name = "USDT", symbol = "₮" }
        });
    }
}
