using Meezan.DataModel.Base;
using Meezan.DataModel.Enums;

namespace Meezan.DataModel.Entities
{
    public class ZakatCycle : BaseEntity
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string HawlStartHijri { get; set; }
        public string HawlDueHijri { get; set; }
        public ZakatCycleStatus Status { get; set; }
        public decimal? PotGoldGramsAtDue { get; set; }
        public decimal? ZakatDueGoldGrams { get; set; }
        public decimal? ExternalPaidGoldGrams { get; set; }
        public Account Account { get; set; }
        public ICollection<Transaction> Payments { get; set; }
    }
}
