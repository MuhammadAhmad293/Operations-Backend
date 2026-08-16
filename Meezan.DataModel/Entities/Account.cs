using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class Account : BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string BaseCurrencyCode { get; set; }
        public DisplayCalendar DisplayCalendar { get; set; }
        public Theme Theme { get; set; }
        public Language Language { get; set; }
        public User User { get; set; }
        public Currency BaseCurrency { get; set; }
    }
}
