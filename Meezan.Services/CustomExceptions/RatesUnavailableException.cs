namespace Meezan.Services.CustomExceptions
{
    public class RatesUnavailableException : Exception
    {
        public RatesUnavailableException(string message = "Rates unavailable") : base(message) { }
    }
}
