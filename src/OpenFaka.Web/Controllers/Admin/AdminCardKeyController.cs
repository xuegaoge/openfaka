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
[Route("admin/faka/card-keys")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminCardKeyController : ControllerBase
{
    private readonly ICardKeyService _cardKeyService;
    private readonly IFreeSql _db;

    public AdminCardKeyController(ICardKeyService cardKeyService, IFreeSql db)
    {
        _cardKeyService = cardKeyService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetCardKeys([FromQuery] long? productId, [FromQuery] int page = 0, [FromQuery] int count = 20, [FromQuery] bool includeContent = false)
    {
        var items = await _cardKeyService.GetCardKeysAsync(productId, page, count, includeContent);
        var total = await _cardKeyService.GetCardKeyCountAsync(productId);
        return Ok(new { list = items, pagination = new { page, page_size = count, total } });
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportCardKeys([FromBody] ImportCardKeysRequest request)
    {
        try
        {
            var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                ?? User.FindFirst("user_id")
                ?? User.FindFirst("sub");
            if (claim == null || !long.TryParse(claim.Value, out var userId))
                return Unauthorized(new { code = -1, message = "无法识别管理员身份" });
            var batch = await _cardKeyService.ImportCardKeysAsync(request, userId);
            return Ok(batch);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { code = -1, message = ex.Message });
        }
    }

    [HttpPut("{id}/void")]
    public async Task<IActionResult> VoidCardKey(long id)
    {
        var result = await _cardKeyService.VoidCardKeyAsync(id);
        if (!result)
            return BadRequest(new { code = -1, message = "Cannot void this card key" });
        return Ok();
    }

    [HttpGet("stock")]
    public async Task<IActionResult> GetStockSummary()
    {
        var summary = await _cardKeyService.GetStockSummaryAsync();
        return Ok(summary);
    }

    [HttpGet("import-batches")]
    public async Task<IActionResult> GetImportBatches([FromQuery] long? productId, [FromQuery] int page = 0, [FromQuery] int count = 20)
    {
        var query = _db.Select<FakaCardImportBatch>()
            .WhereIf(productId.HasValue, b => b.ProductId == productId.Value)
            .OrderByDescending(b => b.CreateTime)
            .Page(page + 1, count);

        var batches = await query.ToListAsync();
        var total = await _db.Select<FakaCardImportBatch>()
            .WhereIf(productId.HasValue, b => b.ProductId == productId.Value)
            .CountAsync();

        var result = batches.Select(b => new
        {
            id = b.Id,
            product_id = b.ProductId,
            total_count = b.TotalCount,
            imported_count = b.SuccessCount,
            duplicate_count = b.FailCount,
            created_by = b.ImportedBy,
            created_at = b.CreateTime
        });

        return Ok(new { list = result, pagination = new { page, page_size = count, total } });
    }

    [HttpPost("batch-invalidate")]
    public async Task<IActionResult> BatchInvalidate([FromQuery] long productId, [FromQuery] long? specId)
    {
        var query = _db.Select<FakaCardKey>()
            .Where(c => c.ProductId == productId && c.Status == CardKeyStatus.Available);

        if (specId.HasValue)
            query = query.Where(c => c.SpecId == specId.Value);

        var keys = await query.ToListAsync();
        var count = 0;

        foreach (var key in keys)
        {
            await _db.Update<FakaCardKey>()
                .Where(c => c.Id == key.Id)
                .Set(c => c.Status, CardKeyStatus.Void)
                .ExecuteAffrowsAsync();
            count++;
        }

        return Ok(new { invalidated_count = count });
    }

    [HttpGet("by-order/{orderNo}")]
    public async Task<IActionResult> GetByOrder(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return NotFound();

        var cardKeys = await _db.Select<FakaCardKey>()
            .Where(c => c.OrderId == order.Id && c.Status == CardKeyStatus.Sold)
            .ToListAsync();

        var result = cardKeys.Select(c => new
        {
            id = c.Id,
            content = c.Content
        });

        return Ok(result);
    }
}
