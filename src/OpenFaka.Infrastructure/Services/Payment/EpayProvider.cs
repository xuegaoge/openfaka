using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services.Payment;

/// <summary>
/// 易支付聚合支付（支付宝/微信）
/// 对应 orion-key 的 EpayServiceImpl
/// </summary>
public class EpayProvider : IPaymentProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EpayProvider> _logger;

    // 从 PaymentChannel.configData 解析的配置
    public string Pid { get; set; }
    public string Key { get; set; }
    public string ApiUrl { get; set; }
    public string NotifyUrl { get; set; }
    public string ReturnUrl { get; set; }

    // 支付类型：alipay / wxpay
    public string PayType { get; set; } = "alipay";

    public EpayProvider(HttpClient httpClient, ILogger<EpayProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request)
    {
        // 动态拼接 return_url
        var dynamicReturnUrl = ReturnUrl + (ReturnUrl.Contains("?") ? "&" : "?") + "orderId=" + request.OrderNo;

        var parameters = new SortedDictionary<string, string>
        {
            ["pid"] = Pid,
            ["type"] = PayType,
            ["out_trade_no"] = request.OrderNo,
            ["notify_url"] = NotifyUrl,
            ["return_url"] = dynamicReturnUrl,
            ["name"] = request.Subject,
            ["money"] = request.Amount.ToString("F2"),
            ["clientip"] = "127.0.0.1",
            ["device"] = "pc"
        };

        var sign = BuildSign(Key, parameters);
        parameters["sign"] = sign;
        parameters["sign_type"] = "MD5";

        _logger.LogInformation("Epay createPayment: outTradeNo={OrderNo}, type={Type}, money={Amount}",
            request.OrderNo, PayType, request.Amount);

        var url = ApiUrl.TrimEnd('/') + "/mapi.php";

        try
        {
            var formData = new FormUrlEncodedContent(parameters);
            var response = await _httpClient.PostAsync(url, formData);
            var responseBody = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Epay API response: {Response}", responseBody);

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeEl) ? codeEl.GetInt32() : -1;
            var msg = root.TryGetProperty("msg", out var msgEl) ? msgEl.GetString() : "";
            var tradeNo = root.TryGetProperty("trade_no", out var tnEl) ? tnEl.GetString() : "";
            var payUrl = root.TryGetProperty("payurl", out var puEl) ? puEl.GetString() : null;
            var qrcode = root.TryGetProperty("qrcode", out var qrEl) ? qrEl.GetString() : null;

            if (code != 1)
            {
                _logger.LogError("Epay API error: code={Code}, msg={Msg}", code, msg);
                return new CreatePaymentResult { Success = false, ErrorMessage = $"支付创建失败：{msg}" };
            }

            return new CreatePaymentResult
            {
                Success = true,
                PaymentUrl = payUrl,
                QrcodeUrl = qrcode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Epay API call failed");
            return new CreatePaymentResult { Success = false, ErrorMessage = "支付创建失败：网络超时" };
        }
    }

    public Task<WebhookVerifyResult> VerifyWebhookAsync(string payload, IDictionary<string, string> headers)
    {
        // 从 query string 解析参数
        var parameters = ParseQueryString(payload);

        if (!parameters.TryGetValue("sign", out var sign))
        {
            return Task.FromResult(new WebhookVerifyResult { Success = false, ErrorMessage = "Missing sign" });
        }

        if (!VerifySign(Key, parameters, sign))
        {
            return Task.FromResult(new WebhookVerifyResult { Success = false, ErrorMessage = "Invalid signature" });
        }

        var tradeStatus = parameters.GetValueOrDefault("trade_status", "");
        if (tradeStatus != "TRADE_SUCCESS")
        {
            return Task.FromResult(new WebhookVerifyResult { Success = false, ErrorMessage = $"Trade status: {tradeStatus}" });
        }

        return Task.FromResult(new WebhookVerifyResult
        {
            Success = true,
            OrderNo = parameters.GetValueOrDefault("out_trade_no", ""),
            TransactionId = parameters.GetValueOrDefault("trade_no", ""),
            Amount = decimal.TryParse(parameters.GetValueOrDefault("money", "0"), out var amt) ? amt : 0
        });
    }

    public async Task<PaymentQueryResult> QueryPaymentAsync(string transactionId)
    {
        var url = ApiUrl.TrimEnd('/') + $"/api.php?act=order&pid={Pid}&key={Key}&out_trade_no={transactionId}";

        try
        {
            var response = await _httpClient.GetStringAsync(url);
            using var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var tradeStatus = root.TryGetProperty("status", out var stEl) ? stEl.GetString() : "";
            var money = root.TryGetProperty("money", out var mEl) ? mEl.GetString() : "0";
            var tradeNo = root.TryGetProperty("trade_no", out var tnEl) ? tnEl.GetString() : "";

            return new PaymentQueryResult
            {
                Success = tradeStatus == "TRADE_SUCCESS",
                Status = tradeStatus,
                Amount = decimal.TryParse(money, out var amt) ? amt : 0,
                TransactionId = tradeNo
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Epay order query failed");
            return new PaymentQueryResult { Success = false, Status = "unknown" };
        }
    }

    /// <summary>
    /// 易支付签名算法（MD5）
    /// 对应 orion-key 的 EpayServiceImpl.buildSign
    /// </summary>
    public static string BuildSign(string merchantKey, SortedDictionary<string, string> parameters)
    {
        // 1. 过滤 sign, sign_type 和空值
        var filtered = parameters
            .Where(kv => kv.Key != "sign" && kv.Key != "sign_type" && !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key);

        // 2. 拼接参数
        var sb = new StringBuilder();
        foreach (var kv in filtered)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(kv.Key).Append('=').Append(kv.Value);
        }

        // 3. 末尾拼接密钥
        sb.Append(merchantKey);

        // 4. MD5 哈希
        return MD5Hash(sb.ToString());
    }

    public static bool VerifySign(string merchantKey, IDictionary<string, string> parameters, string sign)
    {
        if (string.IsNullOrEmpty(sign)) return false;
        var sorted = new SortedDictionary<string, string>(parameters);
        var expected = BuildSign(merchantKey, sorted);
        return expected.Equals(sign, StringComparison.OrdinalIgnoreCase);
    }

    private static string MD5Hash(string input)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLower();
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
                result[kv[0]] = Uri.UnescapeDataString(kv[1]);
        }
        return result;
    }
}
