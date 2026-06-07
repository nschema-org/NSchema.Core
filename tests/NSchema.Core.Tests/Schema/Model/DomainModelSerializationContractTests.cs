using System.Reflection;
using System.Text.Json.Serialization;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.State;

namespace NSchema.Tests.Schema.Model;

/// <summary>
/// Ensures no serialization contract leaks into the domain model:
/// - User-facing serialization is controlled by <see cref="JsonSchemaDocumentSerializer"/>.
/// - State serialization is owned by <see cref="DefaultSchemaStateSerializer"/>.
/// </summary>
public sealed class DomainModelSerializationContractTests
{
    public static TheoryData<Type> DomainTypes()
    {
        var data = new TheoryData<Type>();
        var types = typeof(DatabaseSchema).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(DatabaseSchema).Namespace)
            .Where(t => t is { IsClass: true, IsAbstract: false } || t.IsValueType)
            .ToList();
        data.AddRange(types);
        return data;
    }

    [Theory]
    [MemberData(nameof(DomainTypes))]
    public void DomainMembers_CarryNoPresentationShapingAttributes(Type type)
    {
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            // An unconditional [JsonIgnore] removes a member from *both* serializers equally — it drops
            // derived/computed values (e.g. AllSchemaNames) rather than diverging the two formats, so it
            // is allowed. A *conditional* ignore is presentation terseness and must not live here.
            var condition = property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition;
            (condition is null or JsonIgnoreCondition.Always or JsonIgnoreCondition.Never).ShouldBeTrue(
                $"{type.Name}.{property.Name} uses a conditional [JsonIgnore]; move the omission into " +
                "JsonSchemaDocumentSerializer so it cannot bleed into the state format.");

            property.GetCustomAttribute<JsonPropertyNameAttribute>().ShouldBeNull(
                $"{type.Name}.{property.Name} renames a member via [JsonPropertyName]; naming is a " +
                "per-serializer concern and must not live on the shared domain model.");

            property.GetCustomAttribute<JsonConverterAttribute>().ShouldBeNull(
                $"{type.Name}.{property.Name} pins a converter via attribute; configure converters on " +
                "each serializer's JsonSerializerOptions instead.");
        }
    }
}
