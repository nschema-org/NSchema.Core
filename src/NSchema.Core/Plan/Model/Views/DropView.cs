using NSchema.Model;

namespace NSchema.Plan.Model.Views;

/// <summary>
/// Represents the removal of an existing view from the database schema.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view to be removed.</param>
/// <param name="ViewName">The name of the view to be removed.</param>
/// <param name="IsMaterialized">Whether the view being removed is a materialized view.</param>
public sealed record DropView(SqlIdentifier SchemaName, SqlIdentifier ViewName, bool IsMaterialized = false) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => true;
}
