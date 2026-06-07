using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services.Payment;

/// <summary>
/// BEpusdt USDT 支付（TRC-20/BEP-20）
/// 对应 orion-key 的 BepusdtServiceImpl
/// </summary>
public class BepusdtProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BepusdtProvider> _logger;

    // 从 PaymentChannel.configData 解析的配置
    public string ApiUrl { get; set; }
    public string ApiToken { get; set; }
    public string NotifyUrl { get; set; }
    public string RedirectUrl { get; set; }
    public string TradeType { get; set; } = "usdt.trc20";
    public string Fiat { get; set; } = "CNY";
    public int Timeout { get; set; } = 900;
    public string FixedRate { get; set; }

    public BepusdtProvider(HttpClient httpClient, ILogger<BepusdtProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request)
    {
        // 构建签名参数
        var signParams = new SortedDictionary<string, string>
        {
            ["order_id"] = request.OrderNo,
            ["amount"] = request.Amount.ToString("F2"),
            ["notify_url"] = NotifyUrl,
            ["redirect_url"] = RedirectUrl,
            ["trade_type"] = TradeType,
            ["fiat"] = Fiat,
            ["name"] = request.Subject,
            ["timeout"] = Timeout.ToString()
        };

        if (!string.IsNullOrEmpty(FixedRate))
        {
            signParams["rate"] = FixedRate;
        }

        var signature = BuildSign(ApiToken, signParams);

        // 构建请求体
        var requestBody = new Dictionary<string, object>(signParams.ToDictionary(kv => kv.Key, kv => (object)kv.Value))
        {
            ["amount"] = request.Amount,
            ["timeout"] = (long)Timeout,
            ["signature"] = signature
        };

        var url = ApiUrl.TrimEnd('/') + "/api/v1/order/create-transaction";

        _logger.LogInformation("BEpusdt createPayment: orderId={OrderNo}, amount={Amount}, tradeType={TradeType}",
            request.OrderNo, request.Amount, TradeType);

        try
        {
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("BEpusdt API response: {Response}", responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var statusCode = root.TryGetProperty("status_code", out var scEl) ? scEl.GetInt32() : -1;
            var msg = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : "";

            if (statusCode != 200)
            {
                _logger.LogError("BEpusdt API error: status_code={Code}, message={Msg}", statusCode, msg);
                return new CreatePaymentResult { Success = false, ErrorMessage = $"USDT 支付创建失败：{msg}" };
            }

            var data = root.TryGetProperty("data", out var dataEl) ? dataEl : default;

            var tradeId = data.TryGetProperty("trade_id", out var tiEl) ? tiEl.GetString() : "";
            var walletAddress = data.TryGetProperty("token", out var tkEl) ? tkEl.GetString() : "";
            var cryptoAmount = data.TryGetProperty("actual_amount", out var aaEl) ? aaEl.GetString() : "";
            var paymentUrl = data.TryGetProperty("payment_url", out var puEl) ? puEl.GetString() : "";
            var expirationTime = data.TryGetProperty("expiration_time", out var etEl) ? etEl.GetInt32() : Timeout;

            _logger.LogInformation("BEpusdt payment created: tradeId={TradeId}, wallet={Wallet}, amount={Amount}",
                tradeId, walletAddress, cryptoAmount);

            return new CreatePaymentResult
            {
                Success = true,
                PaymentUrl = paymentUrl,
                QrcodeUrl = walletAddress,
                WalletAddress = walletAddress,
                CryptoAmount = cryptoAmount,
                Chain = TradeType.Contains("trc20", StringComparison.OrdinalIgnoreCase) ? "TRC20" : "BEP20",
                TradeId = tradeId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BEpusdt API call failed");
            return new CreatePaymentResult { Success = false, ErrorMessage = "USDT 支付创建失败：网络超时" };
        }
    }

    public Task<WebhookVerifyResult> VerifyWebhookAsync(string payload, IDictionary<string, string> headers)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            // BEpusdt webhook 通常包含 order_id, amount, signature 等
            var parameters = new SortedDictionary<string, string>();

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "signature")
                {
                    parameters[property.Name] = property.Value.ToString();
                }
            }

            var signature = root.TryGetProperty("signature", out var sigEl) ? sigEl.GetString() : "";

            if (!VerifySign(ApiToken, parameters, signature))
            {
                return Task.FromResult(new WebhookVerifyResult { Success = false, ErrorMessage = "Invalid signature" });
            }

            var orderNo = parameters.GetValueOrDefault("order_id", "");
            var amount = decimal.TryParse(parameters.GetValueOrDefault("amount", "0"), out var amt) ? amt : 0;
            var tradeId = parameters.GetValueOrDefault("trade_id", "");

            return Task.FromResult(new WebhookVerifyResult
            {
                Success = true,
                OrderNo = orderNo,
                TransactionId = tradeId,
                Amount = amount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BEpusdt webhook verification failed");
            return Task.FromResult(new WebhookVerifyResult { Success = false, ErrorMessage = "Webhook 解析失败" });
        }
    }

    public Task<PaymentQueryResult> QueryPaymentAsync(string transactionId)
    {
        // BEpusdt 通常通过 webhook 通知，不主动查询
        return Task.FromResult(new PaymentQueryResult
        {
            Success = false,
            Status = "unknown",
            TransactionId = transactionId
        });
    }

    /// <summary>
    /// BEpusdt 签名算法（兼容易支付签名协议）
    /// 对应 orion-key 的 BepusdtServiceImpl.buildSign
    /// </summary>
    public static string BuildSign(string apiToken, SortedDictionary<string, string> parameters)
    {
        // 1. 过滤 signature, sign, sign_type 和空值
        var filtered = parameters
            .Where(kv => kv.Key != "signature" && kv.Key != "sign" && kv.Key != "sign_type" && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key);

        // 2. 拼接参数
        var sb = new StringBuilder();
        foreach (var kv in filtered)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(kv.Key).Append('=').Append(kv.Value);
        }

        // 3. 末尾直接拼接 apiToken（无 & 分隔符）
        sb.Append(apiToken);

        // 4. MD5 哈希
        return MD5Hash(sb.ToString());
    }

    public static bool VerifySign(string apiToken, SortedDictionary<string, string> parameters, string signature)
    {
        if (string.IsNullOrEmpty(signature)) return false;
        var expected = BuildSign(apiToken, parameters);
        return expected.Equals(signature, StringComparison.OrdinalIgnoreCase);
    }

    private static string MD5Hash(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }
}
