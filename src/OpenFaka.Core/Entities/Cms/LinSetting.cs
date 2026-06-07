using FreeSql.DataAnnotations;

namespace OpenFaka.Core.Entities.Cms;

[Table(Name = "lin_settings")]
public class LinSetting
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    public string? Name { get; set; }
    public string? Value { get; set; }
    public string? ProviderName { get; set; }
    public string? ProviderKey { get; set; }

    // 审计字段
    public bool IsDeleted { get; set; }
    public long CreateUserId { get; set; }
    public DateTime? CreateTime { get; set; }
    public long UpdateUserId { get; set; }
    public DateTime? UpdateTime { get; set; }
}
