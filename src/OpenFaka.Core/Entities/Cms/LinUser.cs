using FreeSql.DataAnnotations;

namespace OpenFaka.Core.Entities.Cms;

/// <summary>
/// 影子实体：映射 lin_user 表，不继承 FullAuditEntity，避免 FreeSql 反射崩溃
/// </summary>
[Table(Name = "lin_user")]
public class LinUser
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    [Column(StringLength = 24)]
    public string? Username { get; set; }

    [Column(StringLength = 24)]
    public string? Nickname { get; set; }

    public string? Avatar { get; set; }

    [Column(StringLength = 100)]
    public string? Email { get; set; }

    public int Active { get; set; } = 1;

    [Column(StringLength = 100)]
    public string? PhoneNumber { get; set; }

    public string? Introduction { get; set; }
    public string? BlogAddress { get; set; }

    [Column(StringLength = 50)]
    public string? JobTitle { get; set; }

    [Column(StringLength = 50)]
    public string? Company { get; set; }

    public DateTime? LastLoginTime { get; set; }

    [Column(StringLength = 200)]
    public string? RefreshToken { get; set; }

    public bool IsEmailConfirmed { get; set; }
    public string? PasswordResetCode { get; set; }

    [Column(StringLength = 100)]
    public string? Salt { get; set; }

    // 审计字段（与 lin_user 表列对应）
    public long CreateUserId { get; set; }
    public DateTime? CreateTime { get; set; }
    public long UpdateUserId { get; set; }
    public DateTime? UpdateTime { get; set; }
    public bool IsDeleted { get; set; }

    // 导航属性
    [Navigate(ManyToMany = typeof(LinUserGroup))]
    public ICollection<LinGroup>? Groups { get; set; }

    [Navigate(nameof(LinUserIdentity.CreateUserId))]
    public ICollection<LinUserIdentity>? Identities { get; set; }
}
