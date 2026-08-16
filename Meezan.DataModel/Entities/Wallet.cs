using Meezan.DataModel.Base;

namespace Meezan.DataModel.Entities
{
    public class Wallet : BaseEntity
    {
        public int Id { get; set; }
        public int AccountId { get; set; }
        public int WalletTypeId { get; set; }
        public string Name { get; set; }
        public string CurrencyCode { get; set; }
        public decimal InitialAmount { get; set; }
        public string? Color { get; set; }
        public string? Icon { get; set; }
        public bool ExcludeFromTotal { get; set; }
        public bool IsArchived { get; set; }
        public Account Account { get; set; }
        public WalletType WalletType { get; set; }
        public Currency Currency { get; set; }
    }
}
