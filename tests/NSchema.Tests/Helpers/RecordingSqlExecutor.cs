using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Helpers;

/// <summary>Records the SQL plan it was asked to execute, without touching a database.</summary>
internal sealed class RecordingSqlExecutor : ISqlExecutor
{
    public SqlPlan? Executed { get; private set; }

    public Task Execute(SqlPlan plan, CancellationToken cancellationToken = default)
    {
        Executed = plan;
        return Task.CompletedTask;
    }
}
