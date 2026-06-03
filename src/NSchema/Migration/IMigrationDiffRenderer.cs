using NSchema.Migration.Diff;

namespace NSchema.Migration;

/// <summary>
/// Renders a structured <see cref="MigrationDiff"/> into a textual representation, such as a
/// Terraform-style summary or a machine-readable format.
/// </summary>
public interface IMigrationDiffRenderer
{
    /// <summary>
    /// Renders the given diff.
    /// </summary>
    /// <param name="diff">The diff to render.</param>
    /// <returns>The rendered representation.</returns>
    string Render(MigrationDiff diff);
}
