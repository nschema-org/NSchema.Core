using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Domain;

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
            new Schema(new SqlIdentifier("app"), Comment: "application schema",
                Tables:
                [
                    new Table(new SqlIdentifier("users"),
                        PrimaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                        Columns: [new Column(new SqlIdentifier("id"), SqlType.BigInt), new Column(new SqlIdentifier("name"), SqlType.VarChar(255))]),
                ],
                Views:
                [
                    new View(new SqlIdentifier("active_users"), new SqlText("SELECT id, name FROM app.users"), Comment: "currently active users",
                        DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))]),
                ]),
        ]);

        var billing = new Database(
        [
            new Schema(new SqlIdentifier("app"),
                Tables:
                [
                    new Table(new SqlIdentifier("invoices"),
                        Columns: [new Column(new SqlIdentifier("id"), SqlType.BigInt), new Column(new SqlIdentifier("amount"), SqlType.Decimal(18, 2))]),
                ],
                Views:
                [
                    new View(new SqlIdentifier("invoice_totals"), new SqlText("SELECT id, amount FROM app.invoices"),
                        DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("invoices"))]),
                ]),
        ]);

        var reporting = new Database(
        [
            new Schema(new SqlIdentifier("reporting"), Comment: "analytics",
                Tables: [new Table(new SqlIdentifier("daily_totals"), Columns: [new Column(new SqlIdentifier("day"), SqlType.Date)])],
                Views:
                [
                    new View(new SqlIdentifier("weekly_rollup"), new SqlText("SELECT day FROM reporting.daily_totals"),
                        DependsOn: [new ViewDependency(new SqlIdentifier("reporting"), new SqlIdentifier("daily_totals"))]),
                ]),
        ]);

        return Verify(DatabaseAggregator.Combine(DatabaseAggregator.Combine(core, billing).Require(), reporting).Require());
    }
}
