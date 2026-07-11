using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Helpers;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Plan.PlanFile;

/// <summary>
/// Reads and writes saved plan files.
/// </summary>
internal class PlanFileWriter : IPlanFileWriter
{
    private static readonly IReadOnlyList<JsonDerivedType> _eventTypes = DerivedTypes(typeof(ScriptEvent));

    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver
        {
            Modifiers = { JsonHelpers.IgnoreComputedProperties, ConfigurePolymorphism },
        },
    };

    private static IReadOnlyList<JsonDerivedType> DerivedTypes(Type baseType) =>
        baseType.Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false } && baseType.IsAssignableFrom(t))
            .Select(t => new JsonDerivedType(t, t.Name))
            .ToList();

    public async Task<PlanFileEnvelope> Read(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return Deserialize(bytes);
    }

    public async Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken)
    {
        var bytes = Serialize(envelope);
        await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken);
    }

    /// <summary>
    /// Serializes an envelope to bytes. Exposed (alongside <see cref="Deserialize"/>) for in-memory round-trip tests.
    /// </summary>
    public ReadOnlyMemory<byte> Serialize(PlanFileEnvelope envelope)
    {
        return JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
    }

    /// <summary>
    /// Deserializes an envelope from bytes.
    /// </summary>
    /// <exception cref="PlanFileDeserializationException">The payload is corrupt, truncated, or could not be deserialized.</exception>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
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
    /// Configures discriminated-union serialization for the one abstract base the plan carries — the
    /// <see cref="ScriptEvent"/> on each script — so it round-trips to its concrete record.
    /// </summary>
    private static void ConfigurePolymorphism(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(ScriptEvent))
        {
            return;
        }

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "$event",
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };

        foreach (var derived in _eventTypes)
        {
            typeInfo.PolymorphismOptions.DerivedTypes.Add(derived);
        }
    }
}
