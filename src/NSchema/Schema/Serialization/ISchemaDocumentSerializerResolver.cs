namespace NSchema.Schema.Serialization;

/// <summary>
/// Resolves registered <see cref="ISchemaDocumentSerializer"/>s.
/// </summary>
public interface ISchemaDocumentSerializerResolver
{
    /// <summary>
    /// The distinct format names that can be resolved, e.g. <c>json</c>, <c>yaml</c>.
    /// </summary>
    IReadOnlyCollection<string> AvailableFormats { get; }

    /// <summary>
    /// Resolves the serializer registered for <paramref name="format"/> (case-insensitive).
    /// </summary>
    /// <param name="format">The format name, e.g. <c>json</c>.</param>
    /// <returns>The serializer for the format.</returns>
    /// <exception cref="InvalidOperationException">No serializer is registered for the format.</exception>
    ISchemaDocumentSerializer ForFormat(string format);

    /// <summary>
    /// Attempts to resolve the serializer registered for <paramref name="format"/> (case-insensitive).
    /// </summary>
    /// <param name="format">The format name, e.g. <c>json</c>.</param>
    /// <param name="serializer">The resolved serializer, or <see langword="null"/> if none is registered.</param>
    /// <returns><see langword="true"/> if a serializer was found; otherwise <see langword="false"/>.</returns>
    bool TryForFormat(string format, out ISchemaDocumentSerializer? serializer);
}
