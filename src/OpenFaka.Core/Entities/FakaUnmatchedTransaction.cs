using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;
using OpenFaka.Core.Enums;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_unmatched_transaction")]
[Index("idx_faka_unmatched_chain_txid", "Chain,Txid", true)]
public class FakaUnmatchedTransaction : FullAuditEntity<long, long>
{
    public long? OrderId { get; set; }

    [Column(StringLength = 200)]
    public string Txid { get; set; }

    [Column(StringLength = 20)]
    public string Chain { get; set; }

    [Column(StringLength = 200)]
    public string OnChainFrom { get; set; }

    [Column(StringLength = 200)]
    public string OnChainTo { get; set; }

    [Column(Precision = 18, Scale = 6)]
    public decimal? OnChainAmount { get; set; }

    [Column(Precision = 18, Scale = 6)]
    public decimal? ExpectedAmount { get; set; }

    [Column(Precision = 18, Scale = 6)]
    public decimal? AmountDiff { get; set; }

    [Column(StringLength = 50)]
    public string Source { get; set; }

    public UnmatchedTransactionStatus Status { get; set; } = UnmatchedTransactionStatus.Submitted;

    [Column(StringLength = 500)]
    public string VerifyReason { get; set; }

    public long? ReviewerId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public DateTime SubmittedAt { get; set; }

}
