using Microsoft.Extensions.Options;
using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Sql;

/// <summary>
/// Covers <see cref="SqlExecutor"/> when no <c>DbDataSource</c> is configured — i.e. the executor was
/// registered (it always is) but no database provider supplied a connection. No container needed.
/// </summary>
public sealed class DefaultSqlExecutorOfflineTests
{
    private static SqlExecutor WithoutDataSource() => new(Options.Create(new SqlOptions()));

    [Fact]
    public async Task Execute_EmptyPlan_DoesNothing_EvenWithoutDataSource()
    {
        // An empty plan never needs a connection, so it must not trip the missing-connection guard.
        await WithoutDataSource().Execute(new SqlPlan([]), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Execute_NonEmptyPlanWithoutDataSource_ThrowsClearError()
    {
        var act = () => WithoutDataSource().Execute(new SqlPlan([new SqlStatement("SELECT 1")]));

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("database connection");
    }
}
