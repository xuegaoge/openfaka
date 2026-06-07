using FreeSql.DataAnnotations;

namespace OpenFaka.Core.Entities.Cms;

/// <summary>
/// 轻量实体：映射 lin_user_group 表，复合主键
/// 注意：不继承任何基类，避免 FreeSql 对 FullAuditEntity 的反射崩溃
/// </summary>
[Table(Name = "lin_user_group")]
public class LinUserGroup
{
    [Column(IsPrimary = true)]
    public long UserId { get; set; }

    [Column(IsPrimary = true)]
    public long GroupId { get; set; }

    // 审计字段
    public bool IsDeleted { get; set; }
    public long CreateUserId { get; set; }
    public DateTime? CreateTime { get; set; }
    public long UpdateUserId { get; set; }
    public DateTime? UpdateTime { get; set; }

    [Navigate(nameof(UserId))]
    public LinUser? User { get; set; }

    [Navigate(nameof(GroupId))]
    public LinGroup? Group { get; set; }
}
