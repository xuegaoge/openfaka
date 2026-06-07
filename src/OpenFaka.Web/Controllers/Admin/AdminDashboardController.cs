using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/dashboard")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminDashboardController : ControllerBase
{
    private readonly IFreeSql _db;

    public AdminDashboardController(IFreeSql db)
    {
        _db = db;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var todayOrders = await _db.Select<FakaOrder>()
            .Where(o => o.CreateTime >= today)
            .CountAsync();

        var todaySales = await _db.Select<FakaOrder>()
            .Where(o => o.CreateTime >= today && o.Status >= OrderStatus.Paid)
            .SumAsync(o => o.ActualAmount);

        var monthOrders = await _db.Select<FakaOrder>()
            .Where(o => o.CreateTime >= monthStart)
            .CountAsync();

        var monthSales = await _db.Select<FakaOrder>()
            .Where(o => o.CreateTime >= monthStart && o.Status >= OrderStatus.Paid)
            .SumAsync(o => o.ActualAmount);

        // 获取低库存商品（库存少于10）
        var lowStockQuery = await _db.Select<FakaCardKey>()
            .Where(c => c.Status == CardKeyStatus.Available)
            .GroupBy(c => c.ProductId)
            .Having(g => g.Count() < 10)
            .ToListAsync(g => new { ProductId = g.Key, Stock = g.Count() });

        var lowStockList = new List<object>();
        foreach (var item in lowStockQuery)
        {
            var product = await _db.Select<FakaProduct>()
                .Where(p => p.Id == item.ProductId)
                .FirstAsync();
            if (product != null)
            {
                lowStockList.Add(new
                {
                    product_id = product.Id,
                    title = product.Title,
                    available_stock = item.Stock,
                    threshold = product.LowStockThreshold > 0 ? product.LowStockThreshold : 10
                });
            }
        }

        return Ok(new
        {
            today_sales = todaySales,
            month_sales = monthSales,
            today_orders = todayOrders,
            month_orders = monthOrders,
            conversion_rate = 0.0,
            today_pv = 0,
            today_uv = 0,
            low_stock_products = lowStockList
        });
    }

    [HttpGet("sales-trend")]
    public async Task<IActionResult> GetSalesTrend([FromQuery] int days = 7)
    {
        var startDate = DateTime.UtcNow.Date.AddDays(-days);

        var orders = await _db.Select<FakaOrder>()
            .Where(o => o.CreateTime >= startDate && o.Status >= OrderStatus.Paid)
            .ToListAsync();

        var trend = Enumerable.Range(0, days)
            .Select(i => startDate.AddDays(i))
            .Select(date => new
            {
                date = date.ToString("yyyy-MM-dd"),
                sales_amount = orders.Where(o => o.CreateTime.Date == date).Sum(o => o.ActualAmount),
                order_count = orders.Count(o => o.CreateTime.Date == date)
            })
            .ToList();

        return Ok(trend);
    }
}
