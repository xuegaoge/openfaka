using FreeSql.DataAnnotations;

namespace OpenFaka.Core.Entities.Cms;

/// <summary>
/// 轻量实体：映射 lin_group 表，不继承 FullAuditEntity
/// </summary>
[Table(Name = "lin_group")]
public class LinGroup
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    public string Name { get; set; } = "";
    public string? Info { get; set; }
    public bool IsStatic { get; set; }
    public int SortCode { get; set; }

    // 审计字段
    public bool IsDeleted { get; set; }
    public long CreateUserId { get; set; }
    public DateTime? CreateTime { get; set; }
    public long UpdateUserId { get; set; }
    public DateTime? UpdateTime { get; set; }

    [Navigate(ManyToMany = typeof(LinUserGroup))]
    public ICollection<LinUser>? Users { get; set; }
}
