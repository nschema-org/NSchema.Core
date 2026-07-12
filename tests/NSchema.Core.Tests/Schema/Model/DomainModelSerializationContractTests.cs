using System.Reflection;
using NSchema.Project.Domain.Models;
using NSchema.Current.Storage;

namespace NSchema.Tests.Schema.Model;

/// <summary>
/// Ensures the domain model carries no serialization contract whatsoever: every JSON shaping
/// decision is owned by a serializer's <c>JsonSerializerOptions</c> — for example state serialization
/// by <see cref="SchemaStateSerializer"/>.
/// An attribute on the shared model would silently bind both formats together, so none is allowed.
/// </summary>
public sealed class DomainModelSerializationContractTests
{
    private const string JsonSerializationNamespace = "System.Text.Json.Serialization";

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
    public void DomainTypes_CarryNoJsonSerializationAttributes(Type type)
    {
        var offenders = JsonAttributesOn(type.Name, type)
            .Concat(type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(p => JsonAttributesOn($"{type.Name}.{p.Name}", p)))
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(f => JsonAttributesOn($"{type.Name}.{f.Name}", f)))
            .Concat(type.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .SelectMany(p => JsonAttributesOn($"{type.Name}(.ctor {p.Name})", p)))
            .ToList();

        offenders.ShouldBeEmpty(
            $"{type.Name} carries System.Text.Json.Serialization attributes that bind a serialization " +
            "contract onto the shared domain model. Configure this on each serializer's " +
            $"JsonSerializerOptions instead: {string.Join(", ", offenders)}");
    }

    private static IEnumerable<string> JsonAttributesOn(string location, ICustomAttributeProvider member) =>
        member.GetCustomAttributes(inherit: false)
            .Where(a => a.GetType().Namespace == JsonSerializationNamespace)
            .Select(a => $"{location} → [{a.GetType().Name}]");
}
