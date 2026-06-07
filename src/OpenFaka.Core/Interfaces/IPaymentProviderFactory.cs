namespace OpenFaka.Core.Interfaces;

public interface IPaymentProviderFactory
{
    IPaymentProvider GetProvider(string providerName);

    /// <summary>
    /// 根据支付渠道代码获取已注入配置的 Provider
    /// </summary>
    Task<IPaymentProvider> GetProviderByChannelCodeAsync(string channelCode);
}
