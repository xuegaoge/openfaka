using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IFreeSql _db;

    public UserController(IFreeSql db)
    {
        _db = db;
    }

    private long? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)
            ?? User.FindFirst("user_id")
            ?? User.FindFirst("sub");
        if (claim != null && long.TryParse(claim.Value, out var userId))
            return userId;
        return null;
    }

    [HttpGet("orders")]
    public async Task<IActionResult> GetMyOrders([FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] string? status = null)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var query = _db.Select<FakaOrder>()
            .Where(o => o.UserId == userId.Value);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
        {
            query = query.Where(o => o.Status == orderStatus);
        }

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreateTime)
            .Page(page + 1, count)
            .ToListAsync();

        var list = orders.Select(o => new
        {
            id = o.OrderNo,
            order_no = o.OrderNo,
            total_amount = o.TotalAmount,
            actual_amount = o.ActualAmount,
            status = o.Status switch
            {
                OrderStatus.Delivering => "paid",
                OrderStatus.Cancelled => "expired",
                OrderStatus.Refunded => "expired",
                _ => o.Status.ToString().ToLower()
            },
            order_type = o.OrderType == 1 ? "cart" : "direct",
            payment_method = o.PaymentMethod,
            created_at = o.CreateTime,
            paid_at = o.PaidAt,
            delivered_at = o.DeliveredAt,
            expires_at = o.ExpiresAt,
            usdt_tx_id = o.UsdtTxId
        });

        return Ok(new { list, pagination = new { page, page_size = count, total } });
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var user = await _db.Select<LinUser>()
            .Where(u => u.Id == userId.Value)
            .FirstAsync();

        if (user == null) return NotFound();

        return Ok(new
        {
            id = user.Id,
            username = user.Username,
            nickname = user.Nickname,
            email = user.Email,
            avatar = user.Avatar,
            active = user.Active.ToString().ToLower(),
            create_time = user.CreateTime
        });
    }
}
