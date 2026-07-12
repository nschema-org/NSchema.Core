using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Schema.Model;

/// <summary>
/// Snapshot coverage for <see cref="DatabaseSchema"/>. The per-case assertions in
/// <see cref="DatabaseSchemaTests"/> pin the merge rules; this captures the whole merged
/// <c>DatabaseSchema</c> graph for several providers at once, so table grouping, ordering, and
/// carried-through detail all show up as a single reviewable diff.
/// </summary>
public sealed class DatabaseSchemaSnapshotTests
{
    [Fact]
    public Task Aggregate_MultipleProviders_MergesIntoSingleGraph()
    {
        // Two providers contribute to "app" (tables and views merged); a third owns "reporting" on its own.
        var core = new DatabaseSchema(
        [
            new SchemaDefinition("app", Comment: "application schema",
                Tables:
                [
                    new Table("users",
                        PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
                        Columns: [new Column("id", SqlType.BigInt), new Column("name", SqlType.VarChar(255))]),
                ],
                Views:
                [
                    new View("active_users", "SELECT id, name FROM app.users", Comment: "currently active users",
                        DependsOn: [new ViewDependency("app", "users")]),
                ]),
        ]);

        var billing = new DatabaseSchema(
        [
            new SchemaDefinition("app",
                Tables:
                [
                    new Table("invoices",
                        Columns: [new Column("id", SqlType.BigInt), new Column("amount", SqlType.Decimal(18, 2))]),
                ],
                Views:
                [
                    new View("invoice_totals", "SELECT id, amount FROM app.invoices",
                        DependsOn: [new ViewDependency("app", "invoices")]),
                ]),
        ]);

        var reporting = new DatabaseSchema(
        [
            new SchemaDefinition("reporting", Comment: "analytics",
                Tables: [new Table("daily_totals", Columns: [new Column("day", SqlType.Date)])],
                Views:
                [
                    new View("weekly_rollup", "SELECT day FROM reporting.daily_totals",
                        DependsOn: [new ViewDependency("reporting", "daily_totals")]),
                ]),
        ]);

        return Verify(SchemaAggregator.Combine(SchemaAggregator.Combine(core, billing).Require(), reporting).Require());
    }
}
