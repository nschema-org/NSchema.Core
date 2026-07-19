using NSchema.Model;

namespace NSchema.Plan.Model.Views;

/// <summary>
/// Represents renaming an existing view.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the view.</param>
/// <param name="OldName">The current name of the view.</param>
/// <param name="NewName">The new name of the view.</param>
/// <param name="IsMaterialized">Whether the view being renamed is a materialized view.</param>
public sealed record RenameView(SqlIdentifier SchemaName, SqlIdentifier OldName, SqlIdentifier NewName, bool IsMaterialized = false) : MigrationAction;
