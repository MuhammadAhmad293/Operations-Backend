using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class Mail : BaseEntity
    {
        public int MailId { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string To { get; set; }
        public string? CC { get; set; }
        public string? BCC { get; set; }
        public int MailStatusId { get; set; }
        public int MailTypeId { get; set; }
        public string? Name { get; set; }
        public string? MobileNumber { get; set; }
        public string? Email { get; set; }
        public DeliveryStatus DeliveryStatus { get; set; } = DeliveryStatus.Pending;
        public int RetryCount { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? SentAt { get; set; }
        public string? LastError { get; set; }
        public MailStatus MailStatus { get; set; }
        public MailType MailType { get; set; }
        public ICollection<MailAttachment> Attachments { get; set; }
    }
}
