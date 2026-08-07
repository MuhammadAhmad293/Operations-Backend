using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Meezan.Filter
{
    public class SwaggerHeaderFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            if (operation.Parameters is null)
                operation.Parameters = new OpenApiParameter[0];

            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Accept-Language",
                In = ParameterLocation.Header,
                Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                }
            });
        }
    }
}
