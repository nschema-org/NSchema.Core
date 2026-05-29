namespace NSchema.Migration.Sql;

/// <summary>
/// An <see cref="ICompiledMigration"/> over a compiled <see cref="SqlPlan"/>.
/// </summary>
/// <param name="sqlPlan">The compiled SQL plan.</param>
/// <param name="sqlExecutor">The executor that runs the SQL plan.</param>
internal sealed class CompiledSqlMigration(SqlPlan sqlPlan, ISqlExecutor sqlExecutor) : ICompiledMigration
{
    /// <inheritdoc />
    public IReadOnlyList<string> Preview { get; } = sqlPlan.Statements.Select(s => s.Sql).ToArray();

    /// <inheritdoc />
    public Task Execute(CancellationToken cancellationToken = default) => sqlExecutor.Execute(sqlPlan, cancellationToken);
}
