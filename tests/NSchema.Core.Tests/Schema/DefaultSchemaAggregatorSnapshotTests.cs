using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

/// <summary>
/// Snapshot coverage for <see cref="DefaultSchemaAggregator"/>. The per-case assertions in
/// <see cref="DefaultSchemaAggregatorTests"/> pin the merge rules; this captures the whole merged
/// <c>DatabaseSchema</c> graph for several providers at once, so table grouping, ordering, and
/// carried-through detail all show up as a single reviewable diff.
/// </summary>
public sealed class DefaultSchemaAggregatorSnapshotTests
{
    private readonly DefaultSchemaAggregator _sut = new();

    [Fact]
    public Task Aggregate_MultipleProviders_MergesIntoSingleGraph()
    {
        // Two providers contribute to "app" (tables merged); a third owns "reporting" on its own.
        var core = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", comment: "application schema", tables:
            [
                Table.Create("users",
                    primaryKey: new PrimaryKey("users_pkey", ["id"]),
                    columns: [Column.Create("id", SqlType.BigInt), Column.Create("name", SqlType.VarChar(255))]),
            ]),
        ]);

        var billing = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("app", tables:
            [
                Table.Create("invoices",
                    columns: [Column.Create("id", SqlType.BigInt), Column.Create("amount", SqlType.Decimal(18, 2))]),
            ]),
        ]);

        var reporting = DatabaseSchema.Create(
        [
            SchemaDefinition.Create("reporting", comment: "analytics",
                tables: [Table.Create("daily_totals", columns: [Column.Create("day", SqlType.Date)])]),
        ]);

        return Verify(_sut.Aggregate([core, billing, reporting]));
    }
}
