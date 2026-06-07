using Microsoft.AspNetCore.Mvc;
using OpenFaka.Application.Services;

namespace OpenFaka.Web.Controllers.Store;

[ApiController]
[Route("store/usdt")]
public class UsdtController : ControllerBase
{
    private readonly UsdtTxidService _usdtService;

    public UsdtController(UsdtTxidService usdtService)
    {
        _usdtService = usdtService;
    }

    [HttpPost("submit-txid")]
    public async Task<IActionResult> SubmitTxid([FromBody] SubmitTxidRequest request)
    {
        var result = await _usdtService.SubmitTxidAsync(request.OrderNo, request.Txid, request.Chain);
        if (result.Success)
            return Ok(result);
        return BadRequest(result);
    }
}

public class SubmitTxidRequest
{
    public string OrderNo { get; set; }
    public string Txid { get; set; }
    public string Chain { get; set; } = "TRC20";
}
