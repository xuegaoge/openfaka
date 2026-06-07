using FreeSql.DataAnnotations;
using IGeekFan.FreeKit.Extras.AuditEntity;
using OpenFaka.Core.Enums;

namespace OpenFaka.Core.Entities;

[Table(Name = "faka_webhook_event")]
[Index("idx_faka_webhook_provider_event", "Provider,EventId", true)]
[Index("idx_faka_webhook_provider_txn", "Provider,TransactionId", true)]
public class FakaWebhookEvent : FullAuditEntity<long, long>
{
    [Column(StringLength = 50)]
    public string Provider { get; set; }

    [Column(StringLength = 200)]
    public string EventId { get; set; }

    [Column(StringLength = 200)]
    public string TransactionId { get; set; }

    [Column(StringLength = 100)]
    public string EventType { get; set; }

    public string Payload { get; set; }

    public WebhookEventStatus Status { get; set; } = WebhookEventStatus.Received;

    public DateTime? ProcessedAt { get; set; }

    public string ErrorMessage { get; set; }

}
