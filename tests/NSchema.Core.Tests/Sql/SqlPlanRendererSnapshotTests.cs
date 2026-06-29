using NSchema.Sql;
using NSchema.Sql.Model;

namespace NSchema.Tests.Sql;

/// <summary>
/// Snapshot coverage for <see cref="SqlPlanRenderer"/>.
/// </summary>
public sealed class SqlPlanRendererSnapshotTests
{
    private readonly SqlPlanRenderer _sut = new();

    [Fact]
    public Task Render_EmptyPlan() => Verify(_sut.Render(new SqlPlan([])));

    [Fact]
    public Task Render_RichPlan()
    {
        var plan = new SqlPlan(
        [
            new SqlStatement("CREATE SCHEMA app"),
            new SqlStatement("CREATE TABLE app.users (\n    id int NOT NULL,\n    name text NOT NULL\n)"),
            new SqlStatement("CREATE INDEX CONCURRENTLY users_name_ix ON app.users (name)", RunOutsideTransaction: true),
            new SqlStatement("ANALYZE app.users"),
        ]);

        return Verify(_sut.Render(plan));
    }
}
