using NSchema.Current.Domain.Models;

namespace NSchema.Current.Storage;

/// <summary>
/// Serializes and deserializes <see cref="SchemaState"/> snapshots for storage in a state backend.
/// </summary>
internal interface ISchemaStateSerializer
{
    /// <summary>
    /// Serializes a state snapshot to a payload for storage.
    /// </summary>
    ReadOnlyMemory<byte> Serialize(SchemaState state);

    /// <summary>
    /// Deserializes a state snapshot from a stored payload.
    /// </summary>
    /// <exception cref="StateDeserializationException">The payload is corrupt, truncated, or otherwise could not be deserialized.</exception>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
    SchemaState Deserialize(ReadOnlyMemory<byte> value);
}
