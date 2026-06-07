using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.Interfaces;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/payment")]
public class PaymentController : ControllerBase
{
    private readonly IOrderService _orderService;

    public PaymentController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    private static readonly HashSet<string> ValidProviders = new(StringComparer.OrdinalIgnoreCase)
        { "epay", "bepusdt", "manual" };

    [HttpPost("callback/{provider}")]
    public async Task<IActionResult> HandleCallback(string provider)
    {
        if (!ValidProviders.Contains(provider))
            return BadRequest(new { code = -1, message = "Invalid provider" });

        // 读取请求体
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        // 构建 payload
        string payload;

        // 如果 body 是 form-urlencoded 格式
        if (!string.IsNullOrEmpty(body) && !body.TrimStart().StartsWith("{"))
        {
            payload = body;
        }
        else if (!string.IsNullOrEmpty(body))
        {
            payload = body;
        }
        else
        {
            // 如果 body 为空，尝试从 query string 构建
            var queryParams = Request.Query
                .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());
            payload = string.Join("&", queryParams.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
        }

        var headers = Request.Headers
            .Where(h => h.Key.StartsWith("X-"))
            .ToDictionary(h => h.Key, h => h.Value.ToString());

        var result = await _orderService.ProcessPaymentCallbackAsync(provider, payload, headers);
        if (result)
            return Ok(new { code = 0, message = "success" });

        return BadRequest(new { code = -1, message = "callback processing failed" });
    }

    [HttpGet("status/{orderNo}")]
    public async Task<IActionResult> GetPaymentStatus(string orderNo)
    {
        var order = await _orderService.GetOrderAsync(orderNo);
        if (order == null)
            return NotFound(new { code = -1, message = "Order not found" });

        return Ok(new { status = order.Status, orderNo = order.OrderNo });
    }
}
