namespace NSchema.Plan.Model;

/// <summary>
/// Represents the removal of an existing view from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view to be removed.</param>
/// <param name="ViewName">The name of the view to be removed.</param>
public sealed record DropView(string SchemaName, string ViewName) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
