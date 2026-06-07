using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.Services;
using LinCms.Entities;

namespace OpenFaka.Web.Controllers.Admin;

[ApiController]
[Route("admin/faka/txid-reviews")]
[Authorize(Roles = LinGroup.Admin)]
public class AdminTxidController : ControllerBase
{
    private readonly UsdtTxidService _usdtService;

    public AdminTxidController(UsdtTxidService usdtService)
    {
        _usdtService = usdtService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingReview([FromQuery] int page = 0, [FromQuery] int count = 20)
    {
        var items = await _usdtService.GetPendingReviewAsync(page, count);
        return Ok(new { items, total = items.Count });
    }

    [HttpPut("{id}/reject")]
    public async Task<IActionResult> RejectTxid(long id, [FromBody] RejectTxidRequest request)
    {
        var reviewerId = long.Parse(User.FindFirst("sub")?.Value ?? "0");
        var result = await _usdtService.RejectTxidAsync(id, request.Reason, reviewerId);
        if (!result)
            return BadRequest(new { code = -1, message = "Cannot reject this TXID" });
        return Ok();
    }

    [HttpPut("{id}/confirm")]
    public async Task<IActionResult> ConfirmTxid(long id, [FromBody] ConfirmTxidRequest request)
    {
        var result = await _usdtService.ConfirmTxidAsync(request.Chain, request.Txid, request.OnChainAmount, request.From, request.To);
        if (!result)
            return BadRequest(new { code = -1, message = "Cannot confirm this TXID" });
        return Ok();
    }
}

public class RejectTxidRequest
{
    public string Reason { get; set; }
}

public class ConfirmTxidRequest
{
    public string Chain { get; set; }
    public string Txid { get; set; }
    public decimal OnChainAmount { get; set; }
    public string From { get; set; }
    public string To { get; set; }
}
