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
                new ColumnDiff("id", ChangeKind.Add, new Column("id", SqlType.BigInt, IsIdentity: true,
                    IdentityOptions: new IdentityOptions(1, 1, 1)), null, null, null, null, null, null),
                new ColumnDiff("name", ChangeKind.Add, new Column("name", SqlType.VarChar(255)), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, "users_name_ix", new TableIndex("users_name_ix", ["name"], IsUnique: true), null)],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Add, "users_pkey", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, "users_age_chk", null)]);

        var modifiedTable = new TableDiff(
            Schema: "app", Name: "orders", Kind: ChangeKind.Modify, RenamedFrom: "purchases",
            Comment: null,
            Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, new Column("legacy_flag", SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Remove, "writers", TablePrivilege.Insert)],
            Indexes: [],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Modify, "orders_pkey", null, new ValueChange<string>("old note", "new note"))],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, "orders_code_uq", null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Remove, "orders_total_chk", null)]);

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

    /// <summary>
    /// A diff exercising every view change kind: an added view, a body replacement, a comment-only change, a
    /// rename, and a removal.
    /// </summary>
    private static DatabaseDiff ViewChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Views:
                [
                    new ViewDiff("app", "active_users", ChangeKind.Add,
                        Definition: new View("active_users", "SELECT id FROM app.users WHERE active"),
                        Comment: new ValueChange<string>(null, "currently active users")),
                    new ViewDiff("app", "daily_totals", ChangeKind.Modify,
                        Definition: new View("daily_totals", "SELECT date, sum(amount) FROM app.sales GROUP BY date")),
                    new ViewDiff("app", "summary", ChangeKind.Modify,
                        Comment: new ValueChange<string>("old summary", "new summary")),
                    new ViewDiff("app", "report", ChangeKind.Modify, RenamedFrom: "legacy_report"),
                    new ViewDiff("app", "stale_view", ChangeKind.Remove),
                ]),
            ]);
    }

    /// <summary>
    /// A diff exercising every enum change kind: an added enum, anchored value additions, a removal/reorder
    /// (requiring a manual recreate), a comment-only change, a rename, and a removal.
    /// </summary>
    private static DatabaseDiff EnumChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Enums:
                [
                    new EnumDiff("app", "order_status", ChangeKind.Add,
                        Definition: new EnumType("order_status", ["pending", "shipped", "delivered"]),
                        Comment: new ValueChange<string>(null, "order lifecycle")),
                    new EnumDiff("app", "priority", ChangeKind.Modify,
                        AddedValues:
                        [
                            new EnumValueAddition("lowest", Before: "low"),
                            new EnumValueAddition("medium", After: "low"),
                            new EnumValueAddition("highest"),
                        ],
                        Values: new ValueChange<IReadOnlyList<string>>(["low", "high"], ["lowest", "low", "medium", "high", "highest"])),
                    new EnumDiff("app", "severity", ChangeKind.Modify,
                        Values: new ValueChange<IReadOnlyList<string>>(["info", "warn", "error"], ["warn", "error"])),
                    new EnumDiff("app", "kind", ChangeKind.Modify,
                        Comment: new ValueChange<string>("old note", "new note")),
                    new EnumDiff("app", "status", ChangeKind.Modify, RenamedFrom: "state"),
                    new EnumDiff("app", "stale_enum", ChangeKind.Remove),
                ]),
            ]);
    }

    /// <summary>
    /// A diff exercising every sequence change kind: an added sequence (with and without options), an options
    /// change, a comment-only change, a rename, and a removal.
    /// </summary>
    private static DatabaseDiff SequenceChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Sequences:
                [
                    new SequenceDiff("app", "order_id", ChangeKind.Add,
                        Definition: new Sequence("order_id",
                            new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MaxValue: 999999, Cache: 10, Cycle: true)),
                        Comment: new ValueChange<string>(null, "order numbers")),
                    new SequenceDiff("app", "invoice_id", ChangeKind.Add,
                        Definition: new Sequence("invoice_id")),
                    new SequenceDiff("app", "ticket_id", ChangeKind.Modify,
                        Options: new ValueChange<SequenceOptions>(
                            new SequenceOptions(StartWith: 1, IncrementBy: 1),
                            new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true))),
                    new SequenceDiff("app", "audit_id", ChangeKind.Modify,
                        Comment: new ValueChange<string>("old note", "new note")),
                    new SequenceDiff("app", "batch_id", ChangeKind.Modify, RenamedFrom: "job_id"),
                    new SequenceDiff("app", "stale_seq", ChangeKind.Remove),
                ]),
            ]);
    }

    [Fact]
    public Task Render_RichDiff_PlainText() => Verify(Render(RichDiff(), colour: false));

    [Fact]
    public Task Render_RichDiff_WithColour() => Verify(Render(RichDiff(), colour: true));

    [Fact]
    public Task Render_ViewChanges_PlainText() => Verify(Render(ViewChangesDiff(), colour: false));

    [Fact]
    public Task Render_EnumChanges_PlainText() => Verify(Render(EnumChangesDiff(), colour: false));

    [Fact]
    public Task Render_SequenceChanges_PlainText() => Verify(Render(SequenceChangesDiff(), colour: false));

    [Fact]
    public Task Render_EmptyDiff() => Verify(Render(new DatabaseDiff([]), colour: false));
}
