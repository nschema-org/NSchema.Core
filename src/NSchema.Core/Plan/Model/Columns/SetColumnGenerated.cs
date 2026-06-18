namespace NSchema.Plan.Model.Columns;

/// <summary>
/// Represents changing the stored generation expression of an existing column (adding, dropping, or replacing a
/// <c>GENERATED ALWAYS AS (expr) STORED</c> clause).
/// </summary>
/// <param name="SchemaName">The name of the schema containing the table.</param>
/// <param name="TableName">The name of the table containing the column.</param>
/// <param name="ColumnName">The name of the column whose generation expression changes.</param>
/// <param name="OldExpression">The current generation expression, or <see langword="null"/> when the column was not generated.</param>
/// <param name="NewExpression">The new generation expression, or <see langword="null"/> when the column becomes a plain column.</param>
public sealed record SetColumnGenerated(
    string SchemaName,
    string TableName,
    string ColumnName,
    string? OldExpression,
    string? NewExpression
) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
