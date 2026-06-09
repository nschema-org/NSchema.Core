using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Plan.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// Serializes and deserializes <see cref="PlanFileEnvelope"/> payloads.
/// </summary>
internal sealed class PlanFileSerializer
{
    private static readonly IReadOnlyList<JsonDerivedType> _actionTypes =
        typeof(MigrationAction).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && typeof(MigrationAction).IsAssignableFrom(t))
            .Select(t => new JsonDerivedType(t, t.Name))
            .ToList();

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { DomainModelJson.IgnoreComputedProperties, ConfigureActionPolymorphism },
        },
    };

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(PlanFileEnvelope envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
    }

    /// <inheritdoc />
    public PlanFileEnvelope Deserialize(ReadOnlyMemory<byte> value)
    {
        PlanFileEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<PlanFileEnvelope>(value.Span, _options);
        }
        catch (Exception ex)
        {
            throw new PlanFileDeserializationException(
                "The plan file could not be deserialized; it may be corrupt, truncated, or written by an incompatible version of NSchema.",
                ex
            );
        }

        if (envelope is null)
        {
            throw new PlanFileDeserializationException("The plan file deserialized to null.");
        }

        if (envelope.Version > PlanFileEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"Plan file version {envelope.Version} is newer than the supported version " +
                $"{PlanFileEnvelope.CurrentVersion}. Upgrade NSchema to read this plan file.");
        }

        return envelope;
    }

    /// <summary>
    /// Configures discriminated-union serialization for the <see cref="MigrationAction"/> base type so the ordered
    /// actions round-trip to their concrete records.
    /// </summary>
    private static void ConfigureActionPolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(MigrationAction))
        {
            return;
        }

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$action",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        foreach (var derived in _actionTypes)
        {
            typeInfo.PolymorphismOptions.DerivedTypes.Add(derived);
        }
    }
}
