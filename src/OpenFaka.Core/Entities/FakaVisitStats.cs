using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_visit_stats")]
public class FakaVisitStats : FullAuditEntity<long, long>
{
    public DateTime Date { get; set; }

    public long Pv { get; set; }

    public long Uv { get; set; }
}
