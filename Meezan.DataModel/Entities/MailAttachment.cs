using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class MailAttachment : BaseEntity
    {
        public int MailAttachmentId { get; set; }
        public string AttachmentPath { get; set; }
        public int MailId { get; set; }
        public Mail Mail { get; set; }
    }
}
