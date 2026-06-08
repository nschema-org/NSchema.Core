using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Provides the current database schema, with optional online (live database) and offline (persisted snapshot) sources.
/// </summary>
internal interface ICurrentSchemaProvider
{
    /// <summary>
    /// Returns the schema for the specified mode.
    /// </summary>
    /// <param name="preferred">The preferred source to retrieve.</param>
    /// <param name="schemaNames">The specific schema names to retrieve; if <see langword="null"/>, retrieves all available schemas.</param>
    /// <param name="required">
    /// When <see langword="true"/> (default), throws if the preferred source is not configured.
    /// When <see langword="false"/>, falls back to the other source if the preferred one is unavailable;
    /// throws only if neither source is configured.
    /// </param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <exception cref="InvalidOperationException">Thrown when the requested source is not available and no fallback exists.</exception>
    ValueTask<DatabaseSchema> GetSchema(SchemaSourceMode preferred, string[]? schemaNames, bool required = true, CancellationToken cancellationToken = default);
}
