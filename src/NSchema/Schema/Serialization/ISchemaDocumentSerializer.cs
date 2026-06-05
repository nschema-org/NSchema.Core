using NSchema.Schema.Model;

namespace NSchema.Schema.Serialization;

/// <summary>
/// Reads and writes a human-authored <see cref="DatabaseSchema"/> document in a particular text format (e.g. JSON or YAML).
/// </summary>
public interface ISchemaDocumentSerializer
{
    /// <summary>
    /// The canonical name of the format this serializer handles, e.g. <c>json</c> or <c>yaml</c>.
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Writes <paramref name="schema"/> to <paramref name="destination"/> in this serializer's format.
    /// </summary>
    /// <param name="schema">The schema to serialize.</param>
    /// <param name="destination">A writable stream to receive the document.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a <see cref="DatabaseSchema"/> from <paramref name="source"/>.
    /// </summary>
    /// <param name="source">A readable stream over the document.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default);
}
