using System.Text.Json;
using FreeSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenFaka.Core.Entities;
using OpenFaka.Core.Enums;
using OpenFaka.Core.Interfaces;

namespace OpenFaka.Infrastructure.Services.Payment;

public class PaymentProviderFactory : IPaymentProviderFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IFreeSql _db;
    private readonly ILogger<PaymentProviderFactory> _logger;

    public PaymentProviderFactory(IServiceProvider serviceProvider, IFreeSql db, ILogger<PaymentProviderFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// 根据支付渠道类型获取 Provider（不注入配置，仅用于 webhook 签名验证等不需要完整配置的场景）
    /// </summary>
    public IPaymentProvider GetProvider(PaymentChannelType channelType)
    {
        return channelType switch
        {
            PaymentChannelType.Alipay or PaymentChannelType.WechatPay =>
                _serviceProvider.GetRequiredService<EpayProvider>() as IPaymentProvider,

            PaymentChannelType.UsdtTrc20 or PaymentChannelType.UsdtErc20 =>
                _serviceProvider.GetRequiredService<BepusdtProvider>() as IPaymentProvider,

            _ => throw new ArgumentException($"Unknown payment channel type: {channelType}")
        };
    }

    /// <summary>
    /// 根据 provider 名称获取 Provider（用于 webhook 回调，不注入配置）
    /// </summary>
    public IPaymentProvider GetProvider(string providerName)
    {
        return providerName?.ToLower() switch
        {
            "epay" or "alipay" or "wxpay" =>
                _serviceProvider.GetRequiredService<EpayProvider>() as IPaymentProvider,

            "bepusdt" or "usdt" =>
                _serviceProvider.GetRequiredService<BepusdtProvider>() as IPaymentProvider,

            _ => throw new ArgumentException($"Unknown payment provider: {providerName}")
        };
    }

    /// <summary>
    /// 根据支付渠道代码获取已注入配置的 Provider
    /// 从数据库读取 FakaPaymentChannel.ConfigData 并解析为 Provider 属性
    /// </summary>
    public async Task<IPaymentProvider> GetProviderByChannelCodeAsync(string channelCode)
    {
        var normalizedChannelCode = NormalizeChannelCode(channelCode);
        var channel = await _db.Select<FakaPaymentChannel>()
            .Where(c => c.ChannelCode == normalizedChannelCode && !c.IsDeleted && c.IsEnabled)
            .FirstAsync();

        if (channel == null)
            throw new InvalidOperationException($"Payment channel '{channelCode}' not found or disabled");

        var config = ParseConfigData(channel.ConfigData);

        return channel.ProviderType switch
        {
            PaymentChannelType.Alipay or PaymentChannelType.WechatPay =>
                CreateEpayProvider(channel.ProviderType, config),

            PaymentChannelType.UsdtTrc20 or PaymentChannelType.UsdtErc20 =>
                CreateBepusdtProvider(config),

            _ => throw new ArgumentException($"Unknown provider type: {channel.ProviderType}")
        };
    }

    private EpayProvider CreateEpayProvider(PaymentChannelType channelType, Dictionary<string, string> config)
    {
        var provider = _serviceProvider.GetRequiredService<EpayProvider>();

        provider.Pid = RequireConfig(config, "pid");
        provider.Key = RequireConfig(config, "key");
        provider.ApiUrl = RequireConfig(config, "api_url");
        provider.NotifyUrl = RequireConfig(config, "notify_url");
        provider.ReturnUrl = RequireConfig(config, "return_url");
        provider.PayType = channelType == PaymentChannelType.Alipay ? "alipay" : "wxpay";

        return provider;
    }

    private BepusdtProvider CreateBepusdtProvider(Dictionary<string, string> config)
    {
        var provider = _serviceProvider.GetRequiredService<BepusdtProvider>();

        provider.ApiUrl = RequireConfig(config, "api_url");
        provider.ApiToken = RequireConfig(config, "api_token");
        provider.NotifyUrl = RequireConfig(config, "notify_url");
        provider.RedirectUrl = config.GetValueOrDefault("redirect_url", "");
        provider.TradeType = config.GetValueOrDefault("trade_type", "usdt.trc20");
        provider.Fiat = config.GetValueOrDefault("fiat", "CNY");
        provider.Timeout = int.TryParse(config.GetValueOrDefault("timeout", "900"), out var t) ? t : 900;
        provider.FixedRate = config.GetValueOrDefault("fixed_rate", "");

        return provider;
    }

    private static string NormalizeChannelCode(string channelCode)
    {
        return channelCode?.Trim().ToLowerInvariant() switch
        {
            "alipay" => "epay_alipay",
            "wechat" or "wxpay" => "epay_wechat",
            _ => channelCode
        };
    }

    private static Dictionary<string, string> ParseConfigData(string configData)
    {
        if (string.IsNullOrWhiteSpace(configData))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(configData)
                   ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    private static string RequireConfig(Dictionary<string, string> config, string key)
    {
        if (!config.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Payment channel config missing required field: '{key}'");
        return value;
    }
}
