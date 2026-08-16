using Meezan.DataModel.Enums;
using Meezan.Dto.DTOs.Category;
using Meezan.Dto.DTOs.Lookup;
using Meezan.Dto.DTOs.Transaction;
using Meezan.Dto.DTOs.Zakat;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Text.Json.Nodes;

namespace Meezan.Filter
{
    public class EnumStringSchemaFilter : ISchemaFilter
    {
        private static readonly Dictionary<(Type DtoType, string PropertyName), Type> EnumBackedProperties = new()
        {
            [(typeof(TransactionDto), nameof(TransactionDto.Type))] = typeof(TransactionType),
            [(typeof(CategoryDto), nameof(CategoryDto.Kind))] = typeof(CategoryKind),
            [(typeof(CurrencyDto), nameof(CurrencyDto.Type))] = typeof(CurrencyType),
            [(typeof(ZakatCycleDto), nameof(ZakatCycleDto.Status))] = typeof(ZakatCycleStatus),
        };

        public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
        {
            foreach (KeyValuePair<(Type DtoType, string PropertyName), Type> entry in EnumBackedProperties)
            {
                if (entry.Key.DtoType != context.Type || schema.Properties is null)
                    continue;

                string? propertyKey = schema.Properties.Keys
                    .FirstOrDefault(k => string.Equals(k, entry.Key.PropertyName, StringComparison.OrdinalIgnoreCase));

                if (propertyKey is null || schema.Properties[propertyKey] is not OpenApiSchema propertySchema)
                    continue;

                propertySchema.Enum = Enum.GetNames(entry.Value)
                    .Select(name => (JsonNode)name)
                    .ToList();
            }
        }
    }
}
