namespace Meezan.Dto.DTOs.Account
{
    public class AccountDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string BaseCurrencyCode { get; set; }
        public string DisplayCalendar { get; set; }
        public string Theme { get; set; }
        public string Language { get; set; }
        public decimal TotalBalance { get; set; }
    }
}
