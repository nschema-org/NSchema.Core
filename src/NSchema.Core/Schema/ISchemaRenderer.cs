using NSchema.Schema.Model;

namespace NSchema.Schema;

/// <summary>
/// Renders a <see cref="DatabaseSchema"/> as human-readable text.
/// </summary>
public interface ISchemaRenderer
{
    /// <summary>
    /// Renders the given schema as human-readable text.
    /// </summary>
    /// <param name="schema">The schema to render.</param>
    /// <returns>The rendered text.</returns>
    string Render(DatabaseSchema schema);
}
