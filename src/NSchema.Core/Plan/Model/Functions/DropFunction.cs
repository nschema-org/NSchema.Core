namespace NSchema.Plan.Model.Functions;

/// <summary>
/// Represents the removal of an existing function from the database schema. With no overloading in the model, a
/// provider emits a bare <c>DROP FUNCTION</c> without a signature.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the function to be removed.</param>
/// <param name="FunctionName">The name of the function to be removed.</param>
public sealed record DropFunction(string SchemaName, string FunctionName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
