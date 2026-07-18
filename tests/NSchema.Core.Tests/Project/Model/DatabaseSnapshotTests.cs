using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Model.Services;

namespace NSchema.Tests.Project.Model;

/// <summary>
/// Snapshot coverage for <see cref="Database"/>. The per-case assertions in
/// <see cref="DatabaseTests"/> pin the merge rules; this captures the whole merged
/// <c>Database</c> graph for several providers at once, so table grouping, ordering, and
/// carried-through detail all show up as a single reviewable diff.
/// </summary>
public sealed class DatabaseSnapshotTests
{
    [Fact]
    public Task Aggregate_MultipleProviders_MergesIntoSingleGraph()
    {
        // Two providers contribute to "app" (tables and views merged); a third owns "reporting" on its own.
        var core = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("app"),
                Tables = [
                    new Table { Name = new SqlIdentifier("users"),
                        PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")] },
                        Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.BigInt }, new Column { Name = new SqlIdentifier("name"), Type = SqlType.VarChar(255) }] },
                ],
                Views = [
                    new View { Name = new SqlIdentifier("active_users"), Body = new SqlText("SELECT id, name FROM app.users"),
                        DependsOn = [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))], Comment = "currently active users" },
                ], Comment = "application schema" },
        ],
        };

        var billing = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("app"),
                Tables = [
                    new Table { Name = new SqlIdentifier("invoices"),
                        Columns = [new Column { Name = new SqlIdentifier("id"), Type = SqlType.BigInt }, new Column { Name = new SqlIdentifier("amount"), Type = SqlType.Decimal(18, 2) }] },
                ],
                Views = [
                    new View { Name = new SqlIdentifier("invoice_totals"), Body = new SqlText("SELECT id, amount FROM app.invoices"),
                        DependsOn = [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("invoices"))] },
                ] },
        ],
        };

        var reporting = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("reporting"),
                Tables = [new Table { Name = new SqlIdentifier("daily_totals"), Columns = [new Column { Name = new SqlIdentifier("day"), Type = SqlType.Date }] }],
                Views = [
                    new View { Name = new SqlIdentifier("weekly_rollup"), Body = new SqlText("SELECT day FROM reporting.daily_totals"),
                        DependsOn = [new ViewDependency(new SqlIdentifier("reporting"), new SqlIdentifier("daily_totals"))] },
                ], Comment = "analytics" },
        ],
        };

        return Verify(DatabaseAggregator.Combine(DatabaseAggregator.Combine(core, billing).Require(), reporting).Require());
    }
}
