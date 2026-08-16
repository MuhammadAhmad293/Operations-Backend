namespace Meezan.Services.CustomExceptions
{
    public class UnprocessableEntityException : Exception
    {
        public UnprocessableEntityException(string message = "Unprocessable entity") : base(message) { }
    }
}
