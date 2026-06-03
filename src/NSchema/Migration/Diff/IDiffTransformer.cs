using NSchema.Migration.Diff.Model;

namespace NSchema.Migration.Diff;

/// <summary>
/// Defines a contract for transforming the structured <see cref="MigrationDiff"/> before it is linearized into an
/// executable plan. Diff transformers reason about the change at the schema/table/column level — for example,
/// collapsing or suppressing changes — and run in registration order.
/// </summary>
public interface IDiffTransformer
{
    /// <summary>
    /// Transforms the given diff, returning the diff that should be linearized.
    /// </summary>
    /// <param name="diff">The structured diff to transform.</param>
    /// <returns>The transformed diff.</returns>
    MigrationDiff Transform(MigrationDiff diff);
}
