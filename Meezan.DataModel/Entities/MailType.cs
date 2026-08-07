using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class MailType : BaseMultilingualTextEntity
    {
        public int MailTypeId { get; set; }
        public ICollection<Mail> Mails { get; set; }
    }
}
