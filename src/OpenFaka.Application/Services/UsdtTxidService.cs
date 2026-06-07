using FreeSql;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;

namespace OpenFaka.Application.Services;

public class UsdtTxidService
{
    private readonly IFreeSql _db;
    private readonly ILogger<UsdtTxidService> _logger;

    public UsdtTxidService(IFreeSql db, ILogger<UsdtTxidService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 提交 USDT TXID 进行验证
    /// </summary>
    public async Task<SubmitTxidResult> SubmitTxidAsync(string orderNo, string txid, string chain)
    {
        // 1. 检查 TXID 是否已存在（去重）
        var existing = await _db.Select<FakaUnmatchedTransaction>()
            .Where(t => t.Chain == chain && t.Txid == txid)
            .FirstAsync();

        if (existing != null)
        {
            _logger.LogInformation("Duplicate TXID submitted: {Chain} {Txid}", chain, txid);
            return new SubmitTxidResult
            {
                Success = false,
                Message = "TXID already submitted",
                Status = existing.Status.ToString().ToLower()
            };
        }

        // 2. 查找订单
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null)
        {
            return new SubmitTxidResult
            {
                Success = false,
                Message = "Order not found"
            };
        }

        // 3. 写入 unmatched_transaction（唯一约束保证去重）
        try
        {
            var unmatched = new FakaUnmatchedTransaction
            {
                OrderId = order.Id,
                Txid = txid,
                Chain = chain,
                ExpectedAmount = order.ActualAmount,
                Source = "user_submit",
                Status = UnmatchedTransactionStatus.Submitted,
                SubmittedAt = DateTime.UtcNow
            };

            await _db.Insert(unmatched).ExecuteAffrowsAsync();

            // 4. 更新订单的 USDT 信息
            await _db.Update<FakaOrder>()
                .Where(o => o.Id == order.Id)
                .Set(o => o.UsdtTxId, txid)
                .Set(o => o.UsdtChain, chain == "TRC20" ? UsdtChainType.Trc20 : UsdtChainType.Erc20)
                .ExecuteAffrowsAsync();

            _logger.LogInformation("TXID submitted: {Chain} {Txid} for order {OrderNo}", chain, txid, orderNo);

            return new SubmitTxidResult
            {
                Success = true,
                Message = "TXID submitted, pending verification"
            };
        }
        catch (Exception ex) when (ex.Message.Contains("UNIQUE") || ex.Message.Contains("unique"))
        {
            // 唯一约束冲突，说明并发提交了相同的 TXID
            _logger.LogWarning("Concurrent duplicate TXID: {Chain} {Txid}", chain, txid);
            return new SubmitTxidResult
            {
                Success = false,
                Message = "TXID already submitted"
            };
        }
    }

    /// <summary>
    /// 确认 TXID（链上验证通过后调用）
    /// </summary>
    public async Task<bool> ConfirmTxidAsync(string chain, string txid, decimal onChainAmount, string from, string to)
    {
        var unmatched = await _db.Select<FakaUnmatchedTransaction>()
            .Where(t => t.Chain == chain && t.Txid == txid)
            .FirstAsync();

        if (unmatched == null)
        {
            _logger.LogWarning("TXID not found for confirmation: {Chain} {Txid}", chain, txid);
            return false;
        }

        if (unmatched.Status == UnmatchedTransactionStatus.Confirmed)
        {
            _logger.LogInformation("TXID already confirmed: {Chain} {Txid}", chain, txid);
            return true; // 幂等
        }

        // 计算金额差异
        var amountDiff = unmatched.ExpectedAmount.HasValue
            ? onChainAmount - unmatched.ExpectedAmount.Value
            : 0m;

        using var uow = _db.CreateUnitOfWork();

        // 更新 unmatched_transaction
        await _db.Update<FakaUnmatchedTransaction>()
            .Where(t => t.Id == unmatched.Id)
            .Set(t => t.Status, UnmatchedTransactionStatus.Confirmed)
            .Set(t => t.OnChainAmount, onChainAmount)
            .Set(t => t.OnChainFrom, from)
            .Set(t => t.OnChainTo, to)
            .Set(t => t.AmountDiff, amountDiff)
            .ExecuteAffrowsAsync();

        // 如果金额匹配，更新订单状态为已支付
        if (Math.Abs(amountDiff) < 0.01m && unmatched.OrderId.HasValue)
        {
            await _db.Update<FakaOrder>()
                .Where(o => o.Id == unmatched.OrderId.Value && o.Status == OrderStatus.Pending)
                .Set(o => o.Status, OrderStatus.Paid)
                .Set(o => o.PaidAt, DateTime.UtcNow)
                .Set(o => o.UsdtCryptoAmount, onChainAmount)
                .ExecuteAffrowsAsync();

            _logger.LogInformation("Order {OrderId} marked as paid via USDT", unmatched.OrderId);
        }
        else
        {
            _logger.LogWarning("TXID amount mismatch: expected {Expected}, got {Actual}, diff {Diff}",
                unmatched.ExpectedAmount, onChainAmount, amountDiff);
        }

        uow.Commit();
        return true;
    }

    /// <summary>
    /// 拒绝 TXID
    /// </summary>
    public async Task<bool> RejectTxidAsync(long id, string reason, long reviewerId)
    {
        var affected = await _db.Update<FakaUnmatchedTransaction>()
            .Where(t => t.Id == id && t.Status == UnmatchedTransactionStatus.Submitted)
            .Set(t => t.Status, UnmatchedTransactionStatus.Rejected)
            .Set(t => t.VerifyReason, reason)
            .Set(t => t.ReviewerId, reviewerId)
            .Set(t => t.ReviewedAt, DateTime.UtcNow)
            .ExecuteAffrowsAsync();

        return affected > 0;
    }

    /// <summary>
    /// 获取待审核的 TXID 列表
    /// </summary>
    public async Task<List<UnmatchedTransactionDto>> GetPendingReviewAsync(int page = 0, int count = 20)
    {
        var items = await _db.Select<FakaUnmatchedTransaction>()
            .Where(t => t.Status == UnmatchedTransactionStatus.Submitted)
            .OrderByDescending(t => t.SubmittedAt)
            .Page(page + 1, count)
            .ToListAsync();

        return items.Select(t => new UnmatchedTransactionDto
        {
            Id = t.Id,
            OrderId = t.OrderId,
            Txid = t.Txid,
            Chain = t.Chain,
            ExpectedAmount = t.ExpectedAmount,
            OnChainAmount = t.OnChainAmount,
            AmountDiff = t.AmountDiff,
            Status = t.Status.ToString().ToLower(),
            SubmittedAt = t.SubmittedAt
        }).ToList();
    }
}

public class SubmitTxidResult
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public string Status { get; set; }
}

public class UnmatchedTransactionDto
{
    public long Id { get; set; }
    public long? OrderId { get; set; }
    public string Txid { get; set; }
    public string Chain { get; set; }
    public decimal? ExpectedAmount { get; set; }
    public decimal? OnChainAmount { get; set; }
    public decimal? AmountDiff { get; set; }
    public string Status { get; set; }
    public DateTime SubmittedAt { get; set; }
}
