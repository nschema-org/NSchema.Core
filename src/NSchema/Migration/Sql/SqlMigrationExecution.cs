namespace NSchema.Migration.Sql;

/// <summary>
/// An <see cref="IMigrationExecution"/> over a compiled <see cref="SqlPlan"/>. Previews as the ordered SQL
/// statements and executes them via the <see cref="ISqlExecutor"/>.
/// </summary>
/// <param name="sqlPlan">The compiled SQL plan.</param>
/// <param name="sqlExecutor">The executor that runs the SQL plan.</param>
internal sealed class SqlMigrationExecution(SqlPlan sqlPlan, ISqlExecutor sqlExecutor) : IMigrationExecution
{
    /// <inheritdoc />
    public IReadOnlyList<string> Preview { get; } = sqlPlan.Statements.Select(s => s.Sql).ToArray();

    /// <inheritdoc />
    public Task Execute(CancellationToken cancellationToken = default) => sqlExecutor.Execute(sqlPlan, cancellationToken);
}
