using Meezan.Services.CustomExceptions;
using Newtonsoft.Json;
using System.Net;

namespace Meezan
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate next;
        public ErrorHandlingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context /* other dependencies */)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception ex)
        {
            var code = HttpStatusCode.InternalServerError; // 500 if unexpected


            if (ex is ObjectNotFoundException) code = HttpStatusCode.NotFound;
            else if (ex is NameRequiredException || ex is ArgumentNullException || ex is InvalidRequestException) code = HttpStatusCode.BadRequest;
            else if (ex is UnprocessableEntityException) code = HttpStatusCode.UnprocessableEntity;
            else if (ex is RatesUnavailableException) code = HttpStatusCode.ServiceUnavailable;

            var result = JsonConvert.SerializeObject(ex.Message);
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)code;
            return context.Response.WriteAsync(result);
        }
    }
}
