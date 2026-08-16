namespace Meezan.Dto.DTOs.Account
{
    public class CreateAccountDto
    {
        public string Name { get; set; }
        public string BaseCurrencyCode { get; set; }
        public decimal? InitialAmount { get; set; }
    }
}
