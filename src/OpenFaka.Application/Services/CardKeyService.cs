using FreeSql;
using Microsoft.Extensions.Logging;
using OpenFaka.Application.DTOs.Admin;
using OpenFaka.Application.Interfaces;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;

namespace OpenFaka.Application.Services;

public class CardKeyService : ICardKeyService
{
    private readonly IFreeSql _db;
    private readonly ILogger<CardKeyService> _logger;

    public CardKeyService(IFreeSql db, ILogger<CardKeyService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CardImportBatch> ImportCardKeysAsync(ImportCardKeysRequest request, long importedBy)
    {
        var product = await _db.Select<FakaProduct>()
            .Where(p => p.Id == request.ProductId)
            .FirstAsync();
        if (product == null)
            throw new InvalidOperationException("Product not found");

        var keys = request.CardKeys ?? new List<string>();
        if (keys.Count == 0)
            throw new InvalidOperationException("卡密列表不能为空");

        var batch = new FakaCardImportBatch
        {
            ProductId = request.ProductId,
            SpecId = request.SpecId,
            ImportedBy = importedBy,
            TotalCount = keys.Count,
            SuccessCount = 0,
            FailCount = 0,
            CreateTime = DateTime.UtcNow
        };
        batch.Id = await _db.Insert(batch).ExecuteIdentityAsync();

        int successCount = 0;
        var failDetails = new List<string>();

        foreach (var key in keys)
        {
            try
            {
                // 检查重复
                var exists = await _db.Select<FakaCardKey>()
                    .Where(c => c.ProductId == request.ProductId
                        && c.Content == key
                        && c.Status != CardKeyStatus.Void)
                    .AnyAsync();

                if (exists)
                {
                    failDetails.Add($"Duplicate: {key[..Math.Min(key.Length, 20)]}...");
                    continue;
                }

                var cardKey = new FakaCardKey
                {
                    ProductId = request.ProductId,
                    SpecId = request.SpecId,
                    Content = key,
                    Status = CardKeyStatus.Available,
                    ImportBatchId = batch.Id,
                    Version = 0
                };
                await _db.Insert(cardKey).ExecuteAffrowsAsync();
                successCount++;
            }
            catch (Exception ex)
            {
                failDetails.Add($"Error: {ex.Message}");
                _logger.LogWarning(ex, "Failed to import card key");
            }
        }

        batch.SuccessCount = successCount;
        batch.FailCount = request.CardKeys.Count - successCount;
        batch.FailDetail = failDetails.Count > 0 ? string.Join("\n", failDetails) : null;
        await _db.Update<FakaCardImportBatch>()
            .Where(b => b.Id == batch.Id)
            .Set(b => b.SuccessCount, successCount)
            .Set(b => b.FailCount, batch.FailCount)
            .Set(b => b.FailDetail, batch.FailDetail)
            .ExecuteAffrowsAsync();

        _logger.LogInformation("Imported {Success}/{Total} card keys for product {ProductId}",
            successCount, request.CardKeys.Count, request.ProductId);

        // 返回完整的导入批次 DTO
        return new CardImportBatch
        {
            Id = batch.Id,
            ProductId = batch.ProductId,
            SpecId = batch.SpecId,
            ImportedBy = importedBy.ToString(),
            TotalCount = batch.TotalCount,
            SuccessCount = batch.SuccessCount,
            FailCount = batch.FailCount,
            FailDetail = batch.FailDetail,
            CreatedAt = batch.CreateTime
        };
    }

    public async Task<List<CardKeyAdminDto>> GetCardKeysAsync(long? productId, int page, int count, bool includeContent = false)
    {
        var query = _db.Select<FakaCardKey>()
            .WhereIf(productId.HasValue, c => c.ProductId == productId.Value)
            .OrderByDescending(c => c.CreateTime)
            .Skip(page * count)
            .Take(count);

        var cardKeys = await query.ToListAsync();

        return cardKeys.Select(c => new CardKeyAdminDto
        {
            Id = c.Id,
            Content = includeContent ? c.Content : null,
            ContentMasked = MaskContent(c.Content),
            Status = c.Status.ToString(),
            SoldAt = c.SoldAt,
            CreatedAt = c.CreateTime
        }).ToList();
    }

    public async Task<int> GetCardKeyCountAsync(long? productId)
    {
        return (int)await _db.Select<FakaCardKey>()
            .WhereIf(productId.HasValue, c => c.ProductId == productId.Value)
            .CountAsync();
    }

    public async Task<bool> VoidCardKeyAsync(long id)
    {
        var cardKey = await _db.Select<FakaCardKey>()
            .Where(c => c.Id == id)
            .FirstAsync();
        if (cardKey == null) return false;

        if (cardKey.Status == CardKeyStatus.Sold || cardKey.Status == CardKeyStatus.Void)
            return false;

        await _db.Update<FakaCardKey>()
            .Where(c => c.Id == id)
            .Set(c => c.Status, CardKeyStatus.Void)
            .ExecuteAffrowsAsync();

        _logger.LogInformation("Card key {Id} voided", id);
        return true;
    }

    public async Task<List<CardKeyStockDto>> GetStockSummaryAsync()
    {
        // 获取所有非删除商品
        var allProducts = await _db.Select<FakaProduct>()
            .Where(p => !p.IsDeleted)
            .ToListAsync(p => new { p.Id, p.Title });

        // 获取每个商品的卡密统计
        var stockData = await _db.Select<FakaCardKey>()
            .Where(c => allProducts.Select(p => p.Id).Contains(c.ProductId))
            .ToListAsync();

        // 按商品分组统计
        var stockDict = stockData
            .GroupBy(c => c.ProductId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    Available = g.Count(c => c.Status == CardKeyStatus.Available),
                    Sold = g.Count(c => c.Status == CardKeyStatus.Sold),
                    Locked = g.Count(c => c.Status == CardKeyStatus.Reserved || c.Status == CardKeyStatus.Locked),
                    Void = g.Count(c => c.Status == CardKeyStatus.Void)
                });

        return allProducts.Select(p =>
        {
            var available = stockDict.TryGetValue(p.Id, out var s) ? s.Available : 0;
            var sold = stockDict.TryGetValue(p.Id, out var s2) ? s2.Sold : 0;
            var locked = stockDict.TryGetValue(p.Id, out var s3) ? s3.Locked : 0;
            var invalid = stockDict.TryGetValue(p.Id, out var s4) ? s4.Void : 0;
            return new CardKeyStockDto
            {
                ProductId = p.Id,
                ProductTitle = p.Title,
                Total = available + sold + locked + invalid,
                Available = available,
                Sold = sold,
                Locked = locked,
                Invalid = invalid
            };
        }).ToList();
    }

    public async Task<List<string>> GetCardKeysForDeliveryAsync(long orderItemId, long productId, long? specId, int quantity)
    {
        using var uow = _db.CreateUnitOfWork();

        // 检查是否已发货（幂等）
        var existing = await _db.Select<FakaCardKey>()
            .Where(c => c.OrderItemId == orderItemId && c.Status == CardKeyStatus.Sold)
            .ToListAsync();
        if (existing.Count > 0)
        {
            return existing.Select(c => c.Content).ToList();
        }

        // 锁定可用卡密（乐观锁）
        var availableKeys = await _db.Select<FakaCardKey>()
            .Where(c => c.ProductId == productId
                && (specId == null || c.SpecId == specId.Value)
                && c.Status == CardKeyStatus.Available)
            .OrderBy(c => c.Id)
            .Take(quantity)
            .ForUpdate() // 行锁
            .ToListAsync();

        if (availableKeys.Count < quantity)
        {
            throw new InvalidOperationException($"Insufficient card keys: need {quantity}, have {availableKeys.Count}");
        }

        // 更新状态
        foreach (var key in availableKeys)
        {
            var affected = await _db.Update<FakaCardKey>()
                .Where(c => c.Id == key.Id && c.Version == key.Version)
                .Set(c => c.Status, CardKeyStatus.Sold)
                .Set(c => c.SoldAt, DateTime.UtcNow)
                .Set(c => c.Version, key.Version + 1)
                .ExecuteAffrowsAsync();

            if (affected == 0)
            {
                throw new InvalidOperationException("Concurrent modification detected on card key");
            }
        }

        uow.Commit();

        return availableKeys.Select(c => c.Content).ToList();
    }

    private string MaskContent(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= 8)
            return "****";
        return content[..4] + "****" + content[^4..];
    }
}
