using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.DTOs.Admin;
using OpenFaka.Application.Interfaces;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/orders")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminOrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IFreeSql _db;

    public AdminOrderController(IOrderService orderService, IFreeSql db)
    {
        _orderService = orderService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] string status = null)
    {
        var query = _db.Select<FakaOrder>()
            .WhereIf(!string.IsNullOrEmpty(status), o => o.Status.ToString().ToLower() == status.ToLower())
            .OrderByDescending(o => o.CreateTime)
            .Page(page + 1, count);

        var orders = await query.ToListAsync();
        var total = await _db.Select<FakaOrder>()
            .WhereIf(!string.IsNullOrEmpty(status), o => o.Status.ToString().ToLower() == status.ToLower())
            .CountAsync();

        // 批量查询所有订单的 items
        var orderIds = orders.Select(o => o.Id).ToList();
        var allItems = orderIds.Count > 0
            ? await _db.Select<FakaOrderItem>()
                .Where(i => orderIds.Contains(i.OrderId))
                .ToListAsync()
            : new List<FakaOrderItem>();

        var result = orders.Select(o =>
        {
            var items = allItems.Where(i => i.OrderId == o.Id).ToList();
            return new OrderAdminDto
            {
                Id = o.OrderNo,
                OrderNo = o.OrderNo,
                Email = o.Email,
                TotalAmount = o.TotalAmount,
                ActualAmount = o.ActualAmount,
                OrderType = o.OrderType,
                Status = o.Status.ToString().ToLower(),
                PaymentMethod = o.PaymentMethod,
                CreatedAt = o.CreateTime,
                PaidAt = o.PaidAt,
                ItemCount = items.Sum(i => i.Quantity),
                IsRiskFlagged = o.IsRiskFlagged,
                Items = items.Select(i => new OrderItemAdminDto
                {
                    ProductTitle = i.ProductTitle,
                    SpecName = i.SpecName,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal
                }).ToList()
            };
        }).ToList();

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> GetOrder(string orderNo)
    {
        var order = await _orderService.GetOrderAsync(orderNo);
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpPut("{orderNo}/mark-paid")]
    public async Task<IActionResult> MarkAsPaid(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null)
            return NotFound();

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { code = -1, message = "Order is not in pending status" });

        await _db.Update<FakaOrder>()
            .Where(o => o.Id == order.Id)
            .Set(o => o.Status, OrderStatus.Paid)
            .Set(o => o.PaidAt, DateTime.UtcNow)
            .ExecuteAffrowsAsync();

        return Ok();
    }
}
