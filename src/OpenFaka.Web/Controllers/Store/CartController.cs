using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.DTOs.Store;
using FreeSql;
using OpenFaka.Core.Entities;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/cart")]
public class CartController : ControllerBase
{
    private readonly IFreeSql _db;

    public CartController(IFreeSql db)
    {
        _db = db;
    }

    private string GetSessionToken()
    {
        return Request.Headers["X-Session-Token"].FirstOrDefault()
            ?? Request.Cookies["session_token"];
    }

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var sessionToken = GetSessionToken();
        if (string.IsNullOrEmpty(sessionToken))
            return Ok(new { items = new List<CartItemDto>(), total_amount = 0m });

        var items = await _db.Select<FakaCartItem>()
            .Where(c => c.SessionToken == sessionToken)
            .ToListAsync();

        var result = new List<CartItemDto>();
        decimal totalAmount = 0;

        foreach (var item in items)
        {
            var product = await _db.Select<FakaProduct>()
                .Where(p => p.Id == item.ProductId)
                .FirstAsync();
            if (product == null) continue;

            string specName = null;
            decimal unitPrice = product.BasePrice;

            if (item.SpecId.HasValue)
            {
                var spec = await _db.Select<FakaProductSpec>()
                    .Where(s => s.Id == item.SpecId.Value)
                    .FirstAsync();
                if (spec != null)
                {
                    specName = spec.Name;
                    unitPrice = spec.Price;
                }
            }

            var subtotal = unitPrice * item.Quantity;
            totalAmount += subtotal;

            var stockCount = await _db.Select<FakaCardKey>()
                .Where(c => c.ProductId == item.ProductId
                    && (item.SpecId == null || c.SpecId == item.SpecId.Value)
                    && c.Status == Core.Enums.CardKeyStatus.Available)
                .CountAsync();

            result.Add(new CartItemDto
            {
                Id = item.Id,
                ProductId = item.ProductId,
                SpecId = item.SpecId,
                ProductTitle = product.Title,
                SpecName = specName,
                CoverUrl = product.CoverUrl,
                Currency = product.CurrencyCode ?? "CNY",
                UnitPrice = unitPrice,
                Quantity = item.Quantity,
                Subtotal = subtotal,
                StockAvailable = (int)stockCount
            });
        }

        return Ok(new { items = result, total_amount = totalAmount });
    }

    [HttpPost]
    public async Task<IActionResult> AddItem([FromBody] AddCartItemRequest request)
    {
        var sessionToken = GetSessionToken();
        if (string.IsNullOrEmpty(sessionToken))
        {
            sessionToken = Guid.NewGuid().ToString("N");
            Response.Headers["X-Session-Token"] = sessionToken;
        }

        // 检查商品是否存在
        var product = await _db.Select<FakaProduct>()
            .Where(p => p.Id == request.ProductId && p.IsEnabled && !p.IsDeleted)
            .FirstAsync();
        if (product == null)
            return BadRequest(new { code = -1, message = "Product not found" });

        // 检查是否已在购物车中
        var existing = await _db.Select<FakaCartItem>()
            .Where(c => c.SessionToken == sessionToken
                && c.ProductId == request.ProductId
                && c.SpecId == request.SpecId)
            .FirstAsync();

        if (existing != null)
        {
            await _db.Update<FakaCartItem>()
                .Where(c => c.Id == existing.Id)
                .Set(c => c.Quantity, existing.Quantity + request.Quantity)
                .ExecuteAffrowsAsync();
        }
        else
        {
            await _db.Insert(new FakaCartItem
            {
                SessionToken = sessionToken,
                ProductId = request.ProductId,
                SpecId = request.SpecId,
                Quantity = request.Quantity
            }).ExecuteAffrowsAsync();
        }

        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdateCartItemRequest request)
    {
        var sessionToken = GetSessionToken();
        if (string.IsNullOrEmpty(sessionToken))
            return BadRequest(new { code = -1, message = "No session" });

        if (request.Quantity <= 0)
        {
            await _db.Delete<FakaCartItem>()
                .Where(c => c.Id == id && c.SessionToken == sessionToken)
                .ExecuteAffrowsAsync();
        }
        else
        {
            await _db.Update<FakaCartItem>()
                .Where(c => c.Id == id && c.SessionToken == sessionToken)
                .Set(c => c.Quantity, request.Quantity)
                .ExecuteAffrowsAsync();
        }

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> RemoveItem(long id)
    {
        var sessionToken = GetSessionToken();
        if (string.IsNullOrEmpty(sessionToken))
            return BadRequest(new { code = -1, message = "No session" });

        await _db.Delete<FakaCartItem>()
            .Where(c => c.Id == id && c.SessionToken == sessionToken)
            .ExecuteAffrowsAsync();

        return Ok();
    }
}
