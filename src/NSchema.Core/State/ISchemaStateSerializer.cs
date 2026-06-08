using NSchema.Schema.Model;

namespace NSchema.State;

/// <summary>
/// Serializes and deserializes <see cref="DatabaseSchema"/> snapshots for storage in a state backend.
/// </summary>
internal interface ISchemaStateSerializer
{
    /// <summary>
    /// Serializes a schema snapshot to a string for storage.
    /// </summary>
    ReadOnlyMemory<byte> Serialize(DatabaseSchema schema);

    /// <summary>
    /// Deserializes a schema snapshot from a stored string.
    /// </summary>
    /// <exception cref="NotSupportedException">The payload was written by an incompatible newer format version.</exception>
    DatabaseSchema Deserialize(ReadOnlyMemory<byte> value);
}
