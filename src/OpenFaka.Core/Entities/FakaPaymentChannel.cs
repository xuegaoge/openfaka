using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;
using OpenFaka.Core.Enums;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_payment_channel")]
public class FakaPaymentChannel : FullAuditEntity<long, long>
{
    [Column(StringLength = 50)]
    public string ChannelCode { get; set; }

    [Column(StringLength = 100)]
    public string ChannelName { get; set; }

    public PaymentChannelType ProviderType { get; set; }

    /// <summary>
    /// 加密存储的配置数据（JSON格式）
    /// </summary>
    public string ConfigData { get; set; }

    public bool IsEnabled { get; set; } = true;

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }
}
