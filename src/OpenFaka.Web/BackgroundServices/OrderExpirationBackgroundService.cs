using FreeSql;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;

namespace OpenFaka.Web.BackgroundServices;

/// <summary>
/// 订单过期自动取消后台任务
/// 定期检查超过 ExpiresAt 的 Pending 订单，自动标记为 Expired
/// 同时释放预留的卡密库存
/// </summary>
public class OrderExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderExpirationBackgroundService> _logger;

    // 检查间隔：每 60 秒
    private static readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    // 每批处理的最大订单数（避免长时间锁定）
    private const int BatchSize = 100;

    public OrderExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<OrderExpirationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderExpirationBackgroundService started.");

        // 启动后等待 30 秒再开始检查，让应用完全启动
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredOrdersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // 正常关闭，忽略
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired orders.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("OrderExpirationBackgroundService stopped.");
    }

    private async Task ProcessExpiredOrdersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IFreeSql>();

        var now = DateTime.UtcNow;

        // 查找已过期的 Pending 订单
        var expiredOrders = await db.Select<FakaOrder>()
            .Where(o => o.Status == OrderStatus.Pending
                     && o.ExpiresAt != null
                     && o.ExpiresAt <= now)
            .OrderBy(o => o.Id)
            .Take(BatchSize)
            .ToListAsync(stoppingToken);

        if (expiredOrders.Count == 0)
            return;

        _logger.LogInformation("Found {Count} expired orders to process.", expiredOrders.Count);

        foreach (var order in expiredOrders)
        {
            stoppingToken.ThrowIfCancellationRequested();

            try
            {
                using var uow = db.CreateUnitOfWork();
                var transaction = uow.GetOrBeginTransaction();

                // 更新订单状态为 Expired
                var affected = await db.Update<FakaOrder>()
                    .WithTransaction(transaction)
                    .Where(o => o.Id == order.Id && o.Status == OrderStatus.Pending)
                    .Set(o => o.Status, OrderStatus.Expired)
                    .ExecuteAffrowsAsync(stoppingToken);

                if (affected == 0)
                {
                    // 订单已被其他进程处理，跳过
                    continue;
                }

                // 释放预留的卡密（将 Reserved 状态的卡密恢复为 Available）
                var releasedKeys = await db.Update<FakaCardKey>()
                    .WithTransaction(transaction)
                    .Where(c => c.OrderId == order.Id && c.Status == CardKeyStatus.Reserved)
                    .Set(c => c.Status, CardKeyStatus.Available)
                    .Set(c => c.OrderId, (long?)null)
                    .Set(c => c.OrderItemId, (long?)null)
                    .ExecuteAffrowsAsync(stoppingToken);

                uow.Commit();

                if (releasedKeys > 0)
                {
                    _logger.LogInformation(
                        "Order {OrderNo} expired. Released {KeyCount} reserved card keys.",
                        order.OrderNo, releasedKeys);
                }
                else
                {
                    _logger.LogInformation("Order {OrderNo} expired.", order.OrderNo);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process expired order {OrderNo}.", order.OrderNo);
            }
        }
    }
}
