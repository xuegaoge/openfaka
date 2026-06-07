namespace OpenFaka.Core.Interfaces;

public interface IPaymentProvider
{
    Task<CreatePaymentResult> CreatePaymentAsync(CreatePaymentRequest request);
    Task<WebhookVerifyResult> VerifyWebhookAsync(string payload, IDictionary<string, string> headers);
    Task<PaymentQueryResult> QueryPaymentAsync(string transactionId);
}

public class CreatePaymentRequest
{
    public string OrderNo { get; set; }
    public decimal Amount { get; set; }
    public string Subject { get; set; }
    public string NotifyUrl { get; set; }
    public string ReturnUrl { get; set; }
}

public class CreatePaymentResult
{
    public bool Success { get; set; }
    public string PaymentUrl { get; set; }
    public string QrcodeUrl { get; set; }
    public string WalletAddress { get; set; }
    public string CryptoAmount { get; set; }
    public string Chain { get; set; }
    public string TradeId { get; set; }
    public string ErrorMessage { get; set; }
}

public class WebhookVerifyResult
{
    public bool Success { get; set; }
    public string OrderNo { get; set; }
    public string TransactionId { get; set; }
    public decimal Amount { get; set; }
    public string ErrorMessage { get; set; }
}

public class PaymentQueryResult
{
    public bool Success { get; set; }
    public string Status { get; set; }
    public decimal Amount { get; set; }
    public string TransactionId { get; set; }
}
