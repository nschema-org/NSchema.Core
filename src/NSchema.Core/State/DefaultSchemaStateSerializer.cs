using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseSchema"/> snapshots to the versioned state envelope.
/// </summary>
internal sealed class DefaultSchemaStateSerializer : ISchemaStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { DomainModelJson.IgnoreComputedProperties } },
    };

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(DatabaseSchema schema)
    {
        var envelope = new SchemaStateEnvelope(SchemaStateEnvelope.CurrentVersion, schema);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
        return bytes;
    }

    /// <inheritdoc />
    public DatabaseSchema Deserialize(ReadOnlyMemory<byte> state)
    {
        var envelope = JsonSerializer.Deserialize<SchemaStateEnvelope>(state.Span, _options)
            ?? throw new JsonException("State payload deserialized to null.");

        if (envelope.Version > SchemaStateEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"State format version {envelope.Version} is newer than the supported version " +
                $"{SchemaStateEnvelope.CurrentVersion}. Upgrade NSchema to read this state.");
        }

        return envelope.Schema;
    }
}
