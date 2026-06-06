using NSchema.Schema.Model;

namespace NSchema.Import;

/// <summary>
/// Writes an imported <see cref="DatabaseSchema"/> to a destination, merging additively with any existing content.
/// </summary>
public interface ISchemaImportTarget
{
    /// <summary>
    /// Writes <paramref name="schema"/> to the target, adding or replacing tables while preserving
    /// any existing content not covered by the import.
    /// </summary>
    Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default);
}
