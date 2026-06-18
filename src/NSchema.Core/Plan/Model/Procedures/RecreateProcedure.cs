using NSchema.Schema.Model.Procedures;

namespace NSchema.Plan.Model.Procedures;

/// <summary>
/// Represents dropping and recreating a procedure whose signature changed: replacing in place under a different
/// argument list would create a separate overload in the database rather than replacing the routine. With no
/// overloading in the model, a provider emits a bare <c>DROP PROCEDURE</c> (no signature) followed by
/// <c>CREATE PROCEDURE</c> — and, since the drop discards the catalog comment, re-issues <c>COMMENT ON</c> from
/// <see cref="Procedure"/>'s <see cref="Schema.Model.Procedures.Procedure.Comment"/> when one is set.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the procedure.</param>
/// <param name="Procedure">The desired procedure to recreate.</param>
public sealed record RecreateProcedure(string SchemaName, Procedure Procedure) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
