using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Current.Domain.Models;

namespace NSchema.Current.Storage;

/// <summary>
/// Serializes and deserializes <see cref="SchemaState"/> snapshots to the versioned state envelope.
/// </summary>
internal sealed class SchemaStateSerializer : ISchemaStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { JsonHelpers.IgnoreComputedProperties } },
    };

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(SchemaState state)
    {
        var envelope = new SchemaStateEnvelope(SchemaStateEnvelope.CurrentVersion, state.Schema)
        {
            Scripts = state.Scripts,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
        return bytes;
    }

    /// <inheritdoc />
    public SchemaState Deserialize(ReadOnlyMemory<byte> state)
    {
        SchemaStateEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<SchemaStateEnvelope>(state.Span, _options);
        }
        catch (Exception ex)
        {
            throw new StateDeserializationException(
                "The stored state payload could not be deserialized; it may be corrupt, truncated, or written by an incompatible version of NSchema.",
                ex
            );
        }

        if (envelope is null)
        {
            throw new StateDeserializationException("State payload deserialized to null.");
        }

        if (envelope.Version > SchemaStateEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"State format version {envelope.Version} is newer than the supported version " +
                $"{SchemaStateEnvelope.CurrentVersion}. Upgrade NSchema to read this state.");
        }

        return new SchemaState(envelope.Schema, envelope.Scripts);
    }
}
