namespace NSchema.Plan.Model;

/// <summary>
/// Represents the removal of an existing procedure from the database schema. With no overloading in the model,
/// a provider emits a bare <c>DROP PROCEDURE</c> without a signature.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the procedure to be removed.</param>
/// <param name="ProcedureName">The name of the procedure to be removed.</param>
public sealed record DropProcedure(string SchemaName, string ProcedureName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
