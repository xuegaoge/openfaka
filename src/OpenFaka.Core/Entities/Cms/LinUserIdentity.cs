using FreeSql.DataAnnotations;

namespace OpenFaka.Core.Entities.Cms;

/// <summary>
/// 轻量实体：映射 lin_user_identity 表，不继承 FullAuditEntity
/// </summary>
[Table(Name = "lin_user_identity")]
public class LinUserIdentity
{
    [Column(IsPrimary = true)]
    public string Id { get; set; } = "";

    public string? IdentityType { get; set; }
    public string? Identifier { get; set; }
    public string? Credential { get; set; }
    public string? ExtraProperties { get; set; }

    // 审计字段
    public bool IsDeleted { get; set; }
    public long CreateUserId { get; set; }
    public DateTime? CreateTime { get; set; }
    public long UpdateUserId { get; set; }
    public DateTime? UpdateTime { get; set; }
}
