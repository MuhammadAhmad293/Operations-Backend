namespace Meezan.Dto.DTOs.Rate
{
    public class RatesResponseDto
    {
        public string BaseCurrencyCode { get; set; }
        public List<RateDto> Rates { get; set; }
    }
}
