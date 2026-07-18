using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace NSchema.Model.Services;

internal static class ModelSerialization
{
    public static JsonSerializerOptions Options => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(), new ValueObjectJsonConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { InheritJsonIgnore }
        }
    };

    // https://github.com/dotnet/runtime/issues/50078#issuecomment-2192460403
    private static void InheritJsonIgnore(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Kind is not JsonTypeInfoKind.Object)
        {
            return;
        }

        for (var i = 0; i < jsonTypeInfo.Properties.Count; i++)
        {
            if (jsonTypeInfo.Properties[i].AttributeProvider is not PropertyInfo propertyInfo)
            {
                continue;
            }

            if (propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() is null)
            {
                continue;
            }

            jsonTypeInfo.Properties.RemoveAt(i--);
        }
    }

}
