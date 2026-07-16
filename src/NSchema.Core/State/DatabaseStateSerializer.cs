using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using NSchema.Model.Services;
using NSchema.State.Domain.Models;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseState"/> snapshots to the versioned state envelope.
/// </summary>
internal sealed class DatabaseStateSerializer : IDatabaseStateSerializer
{
    private static readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Converters = { new JsonStringEnumConverter(), new ValueObjectJsonConverter() },
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { JsonHelpers.IgnoreComputedProperties } },
    };

    /// <inheritdoc />
    public ReadOnlyMemory<byte> Serialize(DatabaseState state)
    {
        var envelope = new DatabaseStateEnvelope(DatabaseStateEnvelope.CurrentVersion, state.Database)
        {
            Scripts = state.Scripts,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, _options);
        return bytes;
    }

    /// <inheritdoc />
    public DatabaseState Deserialize(ReadOnlyMemory<byte> state)
    {
        DatabaseStateEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<DatabaseStateEnvelope>(state.Span, _options);
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

        if (envelope.Version > DatabaseStateEnvelope.CurrentVersion)
        {
            throw new NotSupportedException(
                $"State format version {envelope.Version} is newer than the supported version " +
                $"{DatabaseStateEnvelope.CurrentVersion}. Upgrade NSchema to read this state.");
        }

        return new DatabaseState(envelope.Database, envelope.Scripts);
    }
}
