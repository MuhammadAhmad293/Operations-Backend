using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class RateSnapshot : BaseEntity
    {
        public int Id { get; set; }
        public string FromCurrencyCode { get; set; }
        public string ToCurrencyCode { get; set; }
        public decimal Rate { get; set; }
        public DateTime FetchedAt { get; set; }
        public string Source { get; set; }
        public Currency FromCurrency { get; set; }
        public Currency ToCurrency { get; set; }
    }
}
