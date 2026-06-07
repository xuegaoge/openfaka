using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenFaka.Application.DTOs.Store;
using OpenFaka.Application.Interfaces;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Application.Services;

public class OrderService : IOrderService
{
    private readonly IFreeSql _db;
    private readonly ILogger<OrderService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenFaka.Core.Interfaces.IPaymentProviderFactory _paymentProviderFactory;

    public OrderService(
        IFreeSql db,
        ILogger<OrderService> logger,
        IServiceProvider serviceProvider,
        OpenFaka.Core.Interfaces.IPaymentProviderFactory paymentProviderFactory = null)
    {
        _db = db;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _paymentProviderFactory = paymentProviderFactory;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request, long? userId, string clientIp, int orderType = 0)
    {
        // 幂等检查：idempotency_key
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _db.Select<FakaOrder>()
                .Where(o => o.IdempotencyKey == request.IdempotencyKey)
                .FirstAsync();
            if (existing != null)
            {
                return await MapToOrderDtoAsync(existing);
            }
        }

        // 验证商品和规格
        var orderItems = new List<FakaOrderItem>();
        decimal totalAmount = 0;

        foreach (var item in request.Items)
        {
            var product = await _db.Select<FakaProduct>()
                .Where(p => p.Id == item.ProductId && p.IsEnabled && !p.IsDeleted)
                .FirstAsync();
            if (product == null)
                throw new InvalidOperationException($"Product {item.ProductId} not found or disabled");

            decimal unitPrice = product.BasePrice;
            string specName = null;

            if (item.SpecId.HasValue)
            {
                var spec = await _db.Select<FakaProductSpec>()
                    .Where(s => s.Id == item.SpecId.Value && s.ProductId == item.ProductId && s.IsVisible)
                    .FirstAsync();
                if (spec == null)
                    throw new InvalidOperationException($"Product spec {item.SpecId} not found");
                unitPrice = spec.Price;
                specName = spec.Name;
            }

            // 检查库存
            var availableCount = await _db.Select<FakaCardKey>()
                .Where(c => c.ProductId == item.ProductId
                    && (item.SpecId == null || c.SpecId == item.SpecId.Value)
                    && c.Status == CardKeyStatus.Available)
                .CountAsync();

            if (availableCount < item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for product {product.Title}");

            var subtotal = unitPrice * item.Quantity;
            totalAmount += subtotal;

            orderItems.Add(new FakaOrderItem
            {
                ProductId = item.ProductId,
                SpecId = item.SpecId,
                ProductTitle = product.Title,
                SpecName = specName,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                Subtotal = subtotal,
                CreateTime = DateTime.UtcNow
            });
        }

        // 创建订单
        var now = DateTime.UtcNow;
        var paymentMethod = NormalizePaymentMethodCode(request.PaymentMethod);
        var orderNo = GenerateOrderNo();
        var order = new FakaOrder
        {
            OrderNo = orderNo,
            UserId = userId,
            Email = request.Email,
            TotalAmount = totalAmount,
            ActualAmount = totalAmount,
            Status = OrderStatus.Pending,
            OrderType = orderType,
            PaymentMethod = paymentMethod,
            ExpiresAt = now.AddHours(1),
            IdempotencyKey = request.IdempotencyKey,
            ClientIp = clientIp,
            CreateTime = now
        };

        using var uow = _db.CreateUnitOfWork();
        var orderRepo = uow.GetRepository<FakaOrder>();
        var orderItemRepo = uow.GetRepository<FakaOrderItem>();

        await orderRepo.InsertAsync(order);
        foreach (var item in orderItems)
        {
            item.OrderId = order.Id;
            await orderItemRepo.InsertAsync(item);
        }
        uow.Commit();

        if (_paymentProviderFactory != null && !string.IsNullOrWhiteSpace(order.PaymentMethod))
        {
            var paymentResult = await CreatePaymentAsync(order, orderItems);

            if (!paymentResult.Success)
                throw new InvalidOperationException(paymentResult.ErrorMessage ?? "Payment creation failed");

            var usdtChain = paymentResult.Chain switch
            {
                "TRC20" => UsdtChainType.Trc20,
                "BEP20" => UsdtChainType.Erc20,
                _ => order.UsdtChain
            };
            decimal? parsedCryptoAmount = decimal.TryParse(paymentResult.CryptoAmount, out var cryptoAmount)
                ? cryptoAmount
                : null;

            await _db.Update<FakaOrder>()
                .Where(o => o.Id == order.Id)
                .Set(o => o.PaymentUrl, paymentResult.PaymentUrl)
                .Set(o => o.QrcodeUrl, paymentResult.QrcodeUrl)
                .Set(o => o.UsdtWalletAddress, paymentResult.WalletAddress)
                .Set(o => o.UsdtTradeId, paymentResult.TradeId)
                .Set(o => o.UsdtChain, usdtChain)
                .Set(o => o.UsdtCryptoAmount, parsedCryptoAmount)
                .ExecuteAffrowsAsync();

            order.PaymentUrl = paymentResult.PaymentUrl;
            order.QrcodeUrl = paymentResult.QrcodeUrl;
            order.UsdtWalletAddress = paymentResult.WalletAddress;
            order.UsdtTradeId = paymentResult.TradeId;
            order.UsdtChain = usdtChain;
            order.UsdtCryptoAmount = parsedCryptoAmount;

            if (paymentResult.TradeId?.StartsWith("DEV-", StringComparison.Ordinal) == true)
            {
                await AutoFulfillDevelopmentOrderAsync(order, orderItems);
            }
        }

        _logger.LogInformation("Order created: {OrderNo}, Amount: {Amount}", orderNo, totalAmount);

        return await MapToOrderDtoAsync(order);
    }

    public async Task<OrderDto> GetOrderAsync(string orderNo)
    {
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null) return null;
        return await MapToOrderDtoAsync(order);
    }

    public async Task<List<OrderDto>> GetOrdersByEmailAsync(string email)
    {
        var orders = await _db.Select<FakaOrder>()
            .Where(o => o.Email == email)
            .OrderByDescending(o => o.CreateTime)
            .ToListAsync();
        var result = new List<OrderDto>();
        foreach (var order in orders)
        {
            result.Add(await MapToOrderDtoAsync(order));
        }
        return result;
    }

    public async Task<bool> ProcessPaymentCallbackAsync(string provider, string payload, IDictionary<string, string> headers)
    {
        // 1. 解析订单号和交易金额（支持 JSON 和 form-urlencoded）
        string orderNo = null;
        decimal paidAmount = 0;
        string tradeNo = null;

        try
        {
            // 尝试解析为 JSON
            if (payload.TrimStart().StartsWith("{"))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payload);
                var root = doc.RootElement;

                orderNo = root.TryGetProperty("out_trade_no", out var otn) ? otn.GetString()
                    : root.TryGetProperty("orderNo", out var on) ? on.GetString()
                    : root.TryGetProperty("order_no", out var on2) ? on2.GetString()
                    : root.TryGetProperty("order_id", out var oi) ? oi.GetString()
                    : null;

                paidAmount = root.TryGetProperty("total_amount", out var ta) ? ta.GetDecimal()
                    : root.TryGetProperty("amount", out var am) ? am.GetDecimal()
                    : root.TryGetProperty("totalAmount", out var tam) ? tam.GetDecimal()
                    : 0;

                tradeNo = root.TryGetProperty("trade_no", out var tn) ? tn.GetString()
                    : root.TryGetProperty("transactionId", out var ti) ? ti.GetString()
                    : root.TryGetProperty("transaction_id", out var ti2) ? ti2.GetString()
                    : root.TryGetProperty("trade_id", out var tdi) ? tdi.GetString()
                    : null;
            }
            else
            {
                // 解析 form-urlencoded 格式（易支付回调）
                var parameters = new Dictionary<string, string>();
                foreach (var pair in payload.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=', 2);
                    if (kv.Length == 2)
                        parameters[kv[0]] = Uri.UnescapeDataString(kv[1]);
                }

                orderNo = parameters.GetValueOrDefault("out_trade_no", null)
                    ?? parameters.GetValueOrDefault("orderNo", null)
                    ?? parameters.GetValueOrDefault("order_no", null)
                    ?? parameters.GetValueOrDefault("order_id", null);

                var moneyStr = parameters.GetValueOrDefault("money", "0")
                    ?? parameters.GetValueOrDefault("amount", "0")
                    ?? parameters.GetValueOrDefault("total_amount", "0");
                decimal.TryParse(moneyStr, out paidAmount);

                tradeNo = parameters.GetValueOrDefault("trade_no", null)
                    ?? parameters.GetValueOrDefault("trade_id", null)
                    ?? parameters.GetValueOrDefault("transaction_id", null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse payment callback payload");
        }

        if (string.IsNullOrEmpty(orderNo))
        {
            _logger.LogWarning("Payment callback missing order number: {Provider}", provider);
            return false;
        }

        // 2. 幂等：检查 webhook 事件是否已处理（使用 provider + tradeNo 作为幂等键）
        if (!string.IsNullOrEmpty(tradeNo))
        {
            var existingEvent = await _db.Select<FakaWebhookEvent>()
                .Where(e => e.Provider == provider && e.TransactionId == tradeNo && e.Status == WebhookEventStatus.Processed)
                .FirstAsync();
            if (existingEvent != null)
            {
                _logger.LogInformation("Duplicate webhook ignored: {Provider} {TradeNo}", provider, tradeNo);
                return true;
            }
        }

        // 记录 webhook 事件
        var webhookEvent = new FakaWebhookEvent
        {
            Provider = provider,
            EventId = tradeNo,
            TransactionId = tradeNo,
            Payload = payload,
            Status = WebhookEventStatus.Received
        };
        await _db.Insert(webhookEvent).ExecuteAffrowsAsync();

        // 3. 查找订单（提前查找，用于签名验证和后续处理）
        var order = await _db.Select<FakaOrder>()
            .Where(o => o.OrderNo == orderNo)
            .FirstAsync();
        if (order == null)
        {
            _logger.LogWarning("Order not found: {OrderNo}", orderNo);
            return false;
        }

        // 4. 签名验证（使用订单关联的渠道配置）
        if (_paymentProviderFactory != null && !string.IsNullOrEmpty(order.PaymentMethod))
        {
            try
            {
                var paymentProvider = await _paymentProviderFactory.GetProviderByChannelCodeAsync(order.PaymentMethod);
                var verifyResult = await paymentProvider.VerifyWebhookAsync(payload, headers);
                if (!verifyResult.Success)
                {
                    _logger.LogWarning("Webhook signature verification failed: {Provider}, Error: {Error}", provider, verifyResult.ErrorMessage);
                    return false;
                }
                _logger.LogInformation("Webhook signature verified: {Provider}", provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify webhook signature: {Provider}", provider);
                return false;
            }
        }

        // 5. 事务处理：更新订单状态 + 发货
        using var uow = _db.CreateUnitOfWork();
        var transaction = uow.GetOrBeginTransaction();

        // 幂等：已支付的订单不重复处理
        if (order.Status >= OrderStatus.Paid)
        {
            _logger.LogInformation("Order already paid: {OrderNo}", orderNo);
            return true;
        }

        // 更新订单状态
        await _db.Update<FakaOrder>()
            .WithTransaction(transaction)
            .Where(o => o.Id == order.Id && o.Status == OrderStatus.Pending)
            .Set(o => o.Status, OrderStatus.Paid)
            .Set(o => o.PaidAt, DateTime.UtcNow)
            .Set(o => o.ActualAmount, paidAmount > 0 ? paidAmount : order.ActualAmount)
            .Set(o => o.EpayTradeNo, tradeNo)
            .ExecuteAffrowsAsync();

        // 获取订单项并发货
        var orderItems = await _db.Select<FakaOrderItem>()
            .WithTransaction(transaction)
            .Where(i => i.OrderId == order.Id)
            .ToListAsync();

        foreach (var item in orderItems)
        {
            try
            {
                // 检查是否已发货（幂等）
                var alreadyDelivered = await _db.Select<FakaCardKey>()
                    .WithTransaction(transaction)
                    .Where(c => c.OrderItemId == item.Id && c.Status == CardKeyStatus.Sold)
                    .AnyAsync();

                if (!alreadyDelivered)
                {
                    // 锁定并分配卡密
                    var availableKeys = await _db.Select<FakaCardKey>()
                        .WithTransaction(transaction)
                        .Where(c => c.ProductId == item.ProductId
                            && (item.SpecId == null || c.SpecId == item.SpecId)
                            && c.Status == CardKeyStatus.Available)
                        .OrderBy(c => c.Id)
                        .Take(item.Quantity)
                        .ForUpdate()
                        .ToListAsync();

                    if (availableKeys.Count >= item.Quantity)
                    {
                        foreach (var key in availableKeys)
                        {
                            await _db.Update<FakaCardKey>()
                                .WithTransaction(transaction)
                                .Where(c => c.Id == key.Id && c.Version == key.Version)
                                .Set(c => c.Status, CardKeyStatus.Sold)
                                .Set(c => c.SoldAt, DateTime.UtcNow)
                                .Set(c => c.OrderId, order.Id)
                                .Set(c => c.OrderItemId, item.Id)
                                .Set(c => c.Version, key.Version + 1)
                                .ExecuteAffrowsAsync();
                        }

                        _logger.LogInformation("Delivered {Count} card keys for order item {ItemId}", availableKeys.Count, item.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Insufficient card keys for product {ProductId}: need {Need}, have {Have}",
                            item.ProductId, item.Quantity, availableKeys.Count);
                        await _db.Update<FakaOrder>()
                            .WithTransaction(transaction)
                            .Where(o => o.Id == order.Id)
                            .Set(o => o.Status, OrderStatus.Delivering)
                            .ExecuteAffrowsAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deliver card keys for order item {ItemId}", item.Id);
            }
        }

        // 检查是否全部发货
        var allItems = await _db.Select<FakaOrderItem>()
            .WithTransaction(transaction)
            .Where(i => i.OrderId == order.Id)
            .ToListAsync();
        var allDelivered = true;
        foreach (var item in allItems)
        {
            var deliveredCount = await _db.Select<FakaCardKey>()
                .WithTransaction(transaction)
                .Where(c => c.OrderItemId == item.Id && c.Status == CardKeyStatus.Sold)
                .CountAsync();
            if (deliveredCount < item.Quantity)
            {
                allDelivered = false;
                break;
            }
        }

        if (allDelivered)
        {
            await _db.Update<FakaOrder>()
                .WithTransaction(transaction)
                .Where(o => o.Id == order.Id)
                .Set(o => o.Status, OrderStatus.Delivered)
                .Set(o => o.DeliveredAt, DateTime.UtcNow)
                .ExecuteAffrowsAsync();
        }

        // 标记 webhook 处理完成
        await _db.Update<FakaWebhookEvent>()
            .WithTransaction(transaction)
            .Where(e => e.Id == webhookEvent.Id)
            .Set(e => e.Status, WebhookEventStatus.Processed)
            .Set(e => e.ProcessedAt, DateTime.UtcNow)
            .ExecuteAffrowsAsync();

        uow.Commit();

        _logger.LogInformation("Payment callback processed: {Provider} {OrderNo}", provider, orderNo);
        return true;
    }

    private async Task<OrderDto> MapToOrderDtoAsync(FakaOrder order)
    {
        var items = await _db.Select<FakaOrderItem>()
            .Where(i => i.OrderId == order.Id)
            .ToListAsync();

        var cardKeys = await _db.Select<FakaCardKey>()
            .Where(c => c.OrderId == order.Id)
            .ToListAsync();
        var latestReview = await _db.Select<FakaUnmatchedTransaction>()
            .Where(t => t.OrderId == order.Id)
            .OrderByDescending(t => t.SubmittedAt)
            .FirstAsync();

        return new OrderDto
        {
            Id = order.Id,
            OrderNo = order.OrderNo,
            Email = order.Email,
            TotalAmount = order.TotalAmount,
            ActualAmount = order.ActualAmount,
            Status = order.Status.ToString().ToLower(),
            OrderType = order.OrderType == 1 ? "CART" : "DIRECT",
            PaymentMethod = order.PaymentMethod,
            CreatedAt = order.CreateTime,
            PaidAt = order.PaidAt,
            DeliveredAt = order.DeliveredAt,
            ExpiresAt = order.ExpiresAt,
            PaymentUrl = order.PaymentUrl,
            QrcodeUrl = order.QrcodeUrl,
            PointsDeducted = 0,
            PointsDiscount = 0,
            UsdtTxId = order.UsdtTxId,
            UsdtCryptoAmount = order.UsdtCryptoAmount,
            UsdtChain = order.UsdtChain switch
            {
                UsdtChainType.Trc20 => "TRC20",
                UsdtChainType.Erc20 => order.PaymentMethod?.Contains("bep20", StringComparison.OrdinalIgnoreCase) == true ? "BEP20" : "ERC20",
                _ => null
            },
            TxidReviewStatus = latestReview == null ? null : latestReview.Status switch
            {
                UnmatchedTransactionStatus.Submitted => "PENDING_REVIEW",
                UnmatchedTransactionStatus.Confirmed => "AUTO_APPROVED",
                UnmatchedTransactionStatus.Rejected => "AUTO_REJECTED",
                UnmatchedTransactionStatus.ManualReview => "PENDING_REVIEW",
                _ => null
            },
            TxidReviewReason = latestReview?.VerifyReason,
            Items = items.Select(i => new OrderItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductTitle = i.ProductTitle,
                SpecName = i.SpecName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList(),
            CardKeys = cardKeys.Select(c => new OrderCardKeyDto
            {
                Id = c.Id,
                Content = order.Status == OrderStatus.Delivered ? c.Content : MaskCardKey(c.Content),
                ProductTitle = items.FirstOrDefault(i => i.Id == c.OrderItemId)?.ProductTitle,
                SpecName = items.FirstOrDefault(i => i.Id == c.OrderItemId)?.SpecName,
                Status = c.Status.ToString()
            }).ToList()
        };
    }

    private async Task<CreatePaymentResult> CreatePaymentAsync(FakaOrder order, List<FakaOrderItem> orderItems)
    {
        var env = _serviceProvider.GetService<IHostEnvironment>();
        if (env?.IsDevelopment() == true)
        {
            var isUsdt = order.PaymentMethod.StartsWith("usdt_", StringComparison.OrdinalIgnoreCase);
            var chain = order.PaymentMethod.Contains("trc20", StringComparison.OrdinalIgnoreCase) ? "TRC20" : "BEP20";
            return new CreatePaymentResult
            {
                Success = true,
                PaymentUrl = $"http://localhost:3000/pay/{order.OrderNo}?method={order.PaymentMethod}",
                QrcodeUrl = isUsdt ? "TDEVOPENFAKA1234567890" : $"DEV-PAY-{order.OrderNo}",
                WalletAddress = isUsdt ? "TDEVOPENFAKA1234567890" : null,
                CryptoAmount = isUsdt ? order.ActualAmount.ToString("F2") : null,
                Chain = isUsdt ? chain : null,
                TradeId = $"DEV-{order.OrderNo}"
            };
        }

        try
        {
            var paymentProvider = await _paymentProviderFactory.GetProviderByChannelCodeAsync(order.PaymentMethod);
            var result = await paymentProvider.CreatePaymentAsync(new CreatePaymentRequest
            {
                OrderNo = order.OrderNo,
                Amount = order.ActualAmount,
                Subject = orderItems.Count == 1 ? orderItems[0].ProductTitle : $"OpenFaka Order {order.OrderNo}",
                NotifyUrl = string.Empty,
                ReturnUrl = string.Empty
            });

            if (result.Success)
                return result;

            _logger.LogWarning("Payment creation returned failure for {OrderNo}: {Error}", order.OrderNo, result.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Payment provider creation failed for {OrderNo}", order.OrderNo);
        }

        return new CreatePaymentResult { Success = false, ErrorMessage = "Payment creation failed" };
    }

    private async Task AutoFulfillDevelopmentOrderAsync(FakaOrder order, List<FakaOrderItem> orderItems)
    {
        var now = DateTime.UtcNow;

        foreach (var item in orderItems)
        {
            var availableKeys = await _db.Select<FakaCardKey>()
                .Where(c => c.ProductId == item.ProductId
                    && (item.SpecId == null || c.SpecId == item.SpecId)
                    && c.Status == CardKeyStatus.Available)
                .OrderBy(c => c.Id)
                .Take(item.Quantity)
                .ToListAsync();

            foreach (var key in availableKeys)
            {
                await _db.Update<FakaCardKey>()
                    .Where(c => c.Id == key.Id)
                    .Set(c => c.Status, CardKeyStatus.Sold)
                    .Set(c => c.SoldAt, now)
                    .Set(c => c.OrderId, order.Id)
                    .Set(c => c.OrderItemId, item.Id)
                    .Set(c => c.Version, key.Version + 1)
                    .ExecuteAffrowsAsync();
            }
        }

        await _db.Update<FakaOrder>()
            .Where(o => o.Id == order.Id)
            .Set(o => o.Status, OrderStatus.Delivered)
            .Set(o => o.PaidAt, now)
            .Set(o => o.DeliveredAt, now)
            .ExecuteAffrowsAsync();

        order.Status = OrderStatus.Delivered;
        order.PaidAt = now;
        order.DeliveredAt = now;
    }

    private static string NormalizePaymentMethodCode(string paymentMethod)
    {
        return paymentMethod?.Trim().ToLowerInvariant() switch
        {
            "alipay" => "epay_alipay",
            "wechat" or "wxpay" => "epay_wechat",
            _ => paymentMethod
        };
    }

    private string GenerateOrderNo()
    {
        return DateTime.UtcNow.ToString("yyyyMMddHHmmss") + Random.Shared.Next(100000, 999999).ToString();
    }

    private string MaskCardKey(string content)
    {
        if (string.IsNullOrEmpty(content) || content.Length <= 8)
            return "****";
        return content[..4] + "****" + content[^4..];
    }
}
