using NSchema.Schema.Model;

namespace NSchema.Plan.Model;

/// <summary>
/// Represents dropping and recreating a function whose signature changed: replacing in place under a different
/// argument list would create a separate overload in the database rather than replacing the routine. With no
/// overloading in the model, a provider emits a bare <c>DROP FUNCTION</c> (no signature) followed by
/// <c>CREATE FUNCTION</c> — and, since the drop discards the catalog comment, re-issues <c>COMMENT ON</c> from
/// <see cref="Function"/>'s <see cref="Schema.Model.Function.Comment"/> when one is set.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the function.</param>
/// <param name="Function">The desired function to recreate.</param>
public sealed record RecreateFunction(string SchemaName, Function Function) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
