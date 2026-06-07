using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_category")]
public class FakaCategory : FullAuditEntity<long, long>
{
    [Column(StringLength = 100)]
    public string Name { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }
}
