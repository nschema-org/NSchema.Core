namespace NSchema.Migration;

/// <summary>
/// Provides the current database schema, with optional online (live database) and offline (persisted snapshot) sources.
/// </summary>
public interface ICurrentSchemaProvider
{
    /// <summary>
    /// Returns the schema provider for the specified mode.
    /// </summary>
    /// <param name="preferred">The preferred source to retrieve.</param>
    /// <param name="required">
    /// When <see langword="true"/> (default), throws if the preferred source is not configured.
    /// When <see langword="false"/>, falls back to the other source if the preferred one is unavailable;
    /// throws only if neither source is configured.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the requested source is not available and no fallback exists.</exception>
    ISchemaProvider GetSource(SchemaSourceMode preferred, bool required = true);
}
