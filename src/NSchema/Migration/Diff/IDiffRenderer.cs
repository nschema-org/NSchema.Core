using NSchema.Migration.Diff.Model;

namespace NSchema.Migration.Diff;

/// <summary>
/// Renders a structured <see cref="MigrationDiff"/> into a textual representation.
/// </summary>
public interface IDiffRenderer
{
    /// <summary>
    /// Renders the given diff.
    /// </summary>
    /// <param name="diff">The diff to render.</param>
    /// <returns>The rendered representation.</returns>
    string Render(MigrationDiff diff);
}
