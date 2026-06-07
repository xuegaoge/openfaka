using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.DTOs.Store;
using OpenFaka.Application.Interfaces;
using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/orders")]
public class OrderController : ControllerBase
{
    private readonly IOrderService _orderService;
    private readonly IFreeSql _db;

    public OrderController(IOrderService orderService, IFreeSql db)
    {
        _orderService = orderService;
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

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var order = await _orderService.CreateOrderAsync(request, GetCurrentUserId(), clientIp, 0);
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = -1, message = ex.Message });
        }
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> GetOrder(string orderNo)
    {
        var order = await _orderService.GetOrderAsync(orderNo);
        if (order == null) return NotFound();
        return Ok(order);
    }

    [HttpGet("{orderNo}/status")]
    public async Task<IActionResult> GetOrderStatus(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return NotFound();

        var remainingSeconds = order.ExpiresAt.HasValue
            ? Math.Max(0, (int)(order.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds)
            : 0;

        return Ok(new
        {
            order_id = order.OrderNo,
            status = order.Status.ToString().ToLower(),
            expires_at = order.ExpiresAt,
            remaining_seconds = remainingSeconds,
            payment_url = order.PaymentUrl
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string email, [FromQuery] int page = 0, [FromQuery] int count = 20)
    {
        if (string.IsNullOrEmpty(email))
            return BadRequest(new { code = -1, message = "Email is required" });

        var orders = await _orderService.GetOrdersByEmailAsync(email);
        return Ok(new { list = orders, pagination = new { page, page_size = count, total = orders.Count } });
    }

    [HttpGet("{orderNo}/export")]
    [Authorize(Roles = LinGroup.Admin)]
    public async Task<IActionResult> ExportKeys(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return NotFound();

        if (order.Status < OrderStatus.Delivered)
            return BadRequest(new { code = -1, message = "Order not delivered yet" });

        var cardKeys = await _db.Select<FakaCardKey>()
            .Where(c => c.OrderId == order.Id && c.Status == CardKeyStatus.Sold)
            .ToListAsync();

        return Ok(cardKeys.Select(c => c.Content));
    }

    [HttpPost("deliver")]
    [Authorize(Roles = LinGroup.Admin)]
    public async Task<IActionResult> Deliver([FromBody] DeliverRequest request)
    {
        if (request.OrderIds == null || request.OrderIds.Count == 0)
            return BadRequest(new { code = -1, message = "Order IDs required" });

        var results = new List<object>();
        foreach (var orderNo in request.OrderIds)
        {
            var order = await _db.Select<FakaOrder>()
                .Where(o => o.OrderNo == orderNo)
                .FirstAsync();

            if (order == null)
            {
                results.Add(new { order_no = orderNo, status = "not_found" });
                continue;
            }

            if (order.Status != OrderStatus.Paid)
            {
                results.Add(new { order_no = orderNo, status = order.Status.ToString().ToLower() });
                continue;
            }

            // 触发发货
            var callbackResult = await _orderService.ProcessPaymentCallbackAsync("manual", "{}",
                new Dictionary<string, string> { { "X-Order-No", orderNo } });

            results.Add(new
            {
                order_no = orderNo,
                status = callbackResult ? "delivered" : "failed"
            });
        }

        return Ok(results);
    }

    /// <summary>
    /// 按邮箱/订单号查询订单（前端 order query 页面调用）
    /// </summary>
    [HttpPost("query")]
    public async Task<IActionResult> QueryOrders([FromBody] QueryOrdersRequest request)
    {
        var orders = new List<OrderDto>();

        if (request.OrderIds != null && request.OrderIds.Count > 0)
        {
            foreach (var orderNo in request.OrderIds)
            {
                var order = await _orderService.GetOrderAsync(orderNo);
                if (order != null) orders.Add(order);
            }
        }

        if (request.Emails != null && request.Emails.Count > 0)
        {
            foreach (var email in request.Emails)
            {
                var emailOrders = await _orderService.GetOrdersByEmailAsync(email);
                orders.AddRange(emailOrders.Where(o => orders.All(e => e.OrderNo != o.OrderNo)));
            }
        }

        return Ok(orders);
    }

    /// <summary>
    /// 刷新订单支付状态（前端轮询调用）
    /// </summary>
    [HttpPost("{orderNo}/refresh")]
    public async Task<IActionResult> RefreshOrderStatus(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return NotFound();

        return Ok(new { status = order.Status.ToString().ToLower() });
    }

    /// <summary>
    /// 重新支付（repay）
    /// </summary>
    private static string NormalizePaymentChannelCode(string channelCode)
    {
        return channelCode?.Trim().ToLowerInvariant() switch
        {
            "alipay" => "epay_alipay",
            "wechat" or "wxpay" => "epay_wechat",
            _ => channelCode
        };
    }

    [HttpPost("{orderNo}/repay")]
    public async Task<IActionResult> Repay(string orderNo, [FromBody] RepayRequest request)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return NotFound(new { code = -1, message = "Order not found" });

        if (order.Status != OrderStatus.Pending)
            return BadRequest(new { code = -1, message = "Order is not in pending status" });

        // 检查是否过期
        if (order.ExpiresAt.HasValue && order.ExpiresAt.Value < DateTime.UtcNow)
        {
            await _db.Update<FakaOrder>()
                .Where(o => o.Id == order.Id && o.Status == OrderStatus.Pending)
                .Set(o => o.Status, OrderStatus.Expired)
                .ExecuteAffrowsAsync();
            return BadRequest(new { code = -1, message = "Order has expired" });
        }

        // 获取支付渠道
        var paymentChannelCode = NormalizePaymentChannelCode(order.PaymentMethod);
        var channel = await _db.Select<FakaPaymentChannel>()
            .Where(c => c.ChannelCode == paymentChannelCode && c.IsEnabled && !c.IsDeleted)
            .FirstAsync();
        if (channel == null)
            return BadRequest(new { code = -1, message = "Payment channel not available" });

        // 返回支付信息
        return Ok(new
        {
            order_id = order.OrderNo,
            payment_url = order.PaymentUrl,
            qrcode_url = order.QrcodeUrl,
            pay_url = order.PaymentUrl,
            expires_at = order.ExpiresAt,
            wallet_address = order.UsdtWalletAddress ?? order.QrcodeUrl,
            crypto_amount = order.UsdtCryptoAmount?.ToString(),
            chain = order.UsdtChain?.ToString()?.ToUpper()
        });
    }

    /// <summary>
    /// 从购物车创建订单
    /// </summary>
    [HttpPost("from-cart")]
    public async Task<IActionResult> CreateFromCart([FromBody] CreateFromCartRequest request)
    {
        try
        {
            var sessionToken = Request.Headers["X-Session-Token"].FirstOrDefault()
                ?? Request.Cookies["session_token"];

            if (string.IsNullOrEmpty(sessionToken))
                return BadRequest(new { code = -1, message = "Cart is empty" });

            var cartItems = await _db.Select<FakaCartItem>()
                .Where(c => c.SessionToken == sessionToken)
                .ToListAsync();

            if (cartItems.Count == 0)
                return BadRequest(new { code = -1, message = "Cart is empty" });

            var orderRequest = new CreateOrderRequest
            {
                Email = request.Email,
                PaymentMethod = request.PaymentMethod,
                IdempotencyKey = request.IdempotencyKey,
                Items = cartItems.Select(c => new OrderItemRequest
                {
                    ProductId = c.ProductId,
                    SpecId = c.SpecId,
                    Quantity = c.Quantity
                }).ToList()
            };

            var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var order = await _orderService.CreateOrderAsync(orderRequest, GetCurrentUserId(), clientIp, 1);

            // 清空购物车
            await _db.Delete<FakaCartItem>()
                .Where(c => c.SessionToken == sessionToken)
                .ExecuteAffrowsAsync();

            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = -1, message = ex.Message });
        }
    }
}

public class DeliverRequest
{
    public List<string> OrderIds { get; set; }
}

public class QueryOrdersRequest
{
    public List<string> OrderIds { get; set; } = new();
    public List<string> Emails { get; set; } = new();
}

public class CreateFromCartRequest
{
    public string Email { get; set; }
    public string PaymentMethod { get; set; }
    public string IdempotencyKey { get; set; }
    public string Device { get; set; }
}

public class RepayRequest
{
    public string Device { get; set; }
}
