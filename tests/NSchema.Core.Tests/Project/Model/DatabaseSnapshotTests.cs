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
        var core = new Database(
        [
            new Schema(new SqlIdentifier("app"), 
                tables:
                [
                    new Table(new SqlIdentifier("users"),
                        primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        columns: [new Column(new SqlIdentifier("id"), SqlType.BigInt), new Column(new SqlIdentifier("name"), SqlType.VarChar(255))]),
                ],
                views:
                [
                    new View(new SqlIdentifier("active_users"), new SqlText("SELECT id, name FROM app.users"), 
                        dependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))]) { Comment = "currently active users" },
                ]) { Comment = "application schema" },
        ]);

        var billing = new Database(
        [
            new Schema(new SqlIdentifier("app"),
                tables:
                [
                    new Table(new SqlIdentifier("invoices"),
                        columns: [new Column(new SqlIdentifier("id"), SqlType.BigInt), new Column(new SqlIdentifier("amount"), SqlType.Decimal(18, 2))]),
                ],
                views:
                [
                    new View(new SqlIdentifier("invoice_totals"), new SqlText("SELECT id, amount FROM app.invoices"),
                        dependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("invoices"))]),
                ]),
        ]);

        var reporting = new Database(
        [
            new Schema(new SqlIdentifier("reporting"), 
                tables: [new Table(new SqlIdentifier("daily_totals"), columns: [new Column(new SqlIdentifier("day"), SqlType.Date)])],
                views:
                [
                    new View(new SqlIdentifier("weekly_rollup"), new SqlText("SELECT day FROM reporting.daily_totals"),
                        dependsOn: [new ViewDependency(new SqlIdentifier("reporting"), new SqlIdentifier("daily_totals"))]),
                ]) { Comment = "analytics" },
        ]);

        return Verify(DatabaseAggregator.Combine(DatabaseAggregator.Combine(core, billing).Require(), reporting).Require());
    }
}
