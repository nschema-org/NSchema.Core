using NSchema.Schema.Model;

namespace NSchema.Import;

/// <summary>
/// Writes an imported <see cref="DatabaseSchema"/> to a destination, merging additively with any existing content.
/// </summary>
public interface ISchemaImportTarget
{
    /// <summary>
    /// Gets the name of the target.
    /// </summary>
    public string Target { get; }
    
    /// <summary>
    /// Writes <paramref name="schema"/> to the target.
    /// </summary>
    Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default);
}
