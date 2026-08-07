using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class OutboxMessage : BaseEntity
    {
        public int Id { get; set; }
        public int MailId { get; set; }
        public OutboxStatus Status { get; set; } = OutboxStatus.Pending;
        public DateTime? PublishedAt { get; set; }
        public int RetryCount { get; set; }
        public string? LastError { get; set; }
        public DateTime? LastAttempt { get; set; }

        public Mail Mail { get; set; }
    }
}