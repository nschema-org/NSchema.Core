using Microsoft.Extensions.Options;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Schema.Model;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="TerraformDiffRenderer"/>.
/// </summary>
public sealed class TerraformDiffRendererSnapshotTests
{
    private static string Render(DatabaseDiff diff, bool colour)
        => new TerraformDiffRenderer(Options.Create(new TerraformDiffRendererOptions { IncludeColour = colour })).Render(diff);

    /// <summary>
    /// A diff exercising add/modify/remove across schemas, tables, columns, indexes, constraints, and grants.
    /// </summary>
    private static DatabaseDiff RichDiff()
    {
        var addedTable = new TableDiff(
            Schema: "app", Name: "users", Kind: ChangeKind.Add, RenamedFrom: null,
            Comment: new ValueChange<string>(null, "all users"),
            Columns:
            [
                new ColumnDiff("id", ChangeKind.Add, Column.Create("id", SqlType.BigInt, isIdentity: true,
                    identityOptions: new IdentityOptions(1, 1, 1)), null, null, null, null, null, null),
                new ColumnDiff("name", ChangeKind.Add, Column.Create("name", SqlType.VarChar(255)), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, "users_name_ix", TableIndex.Create("users_name_ix", ["name"], isUnique: true), null)],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", null)]);

        var modifiedTable = new TableDiff(
            Schema: "app", Name: "orders", Kind: ChangeKind.Modify, RenamedFrom: "purchases",
            Comment: null,
            Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, Column.Create("legacy_flag", SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Remove, "writers", TablePrivilege.Insert)],
            Indexes: [],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)]);

        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("reporting", ChangeKind.Add, null,
                    Comment: new ValueChange<string>(null, "analytics"),
                    Grants: [new GrantChange(ChangeKind.Add, "analyst", null)],
                    Tables: []),
                new SchemaDiff("app", null, null, null, [], [addedTable, modifiedTable]),
                new SchemaDiff("scratch", ChangeKind.Remove, null, null, [], []),
            ]);
    }

    [Fact]
    public Task Render_RichDiff_PlainText() => Verify(Render(RichDiff(), colour: false));

    [Fact]
    public Task Render_RichDiff_WithColour() => Verify(Render(RichDiff(), colour: true));

    [Fact]
    public Task Render_EmptyDiff() => Verify(Render(new DatabaseDiff([]), colour: false));
}
