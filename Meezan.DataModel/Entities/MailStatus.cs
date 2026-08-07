using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class MailStatus : BaseMultilingualTextEntity
    {
        public int MailStatusId { get; set; }
        public ICollection<Mail> Mails { get; set; }
    }
}
