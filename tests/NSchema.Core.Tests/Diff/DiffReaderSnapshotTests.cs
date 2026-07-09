using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.CompositeTypes;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Domains;
using NSchema.Schema.Model.Enums;
using NSchema.Schema.Model.Extensions;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Routines;
using NSchema.Schema.Model.Sequences;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Triggers;
using NSchema.Schema.Model.Views;

namespace NSchema.Tests.Diff;

/// <summary>
/// Snapshot coverage for <see cref="DiffReader"/>.
/// </summary>
public sealed class DiffReaderSnapshotTests
{
    private static DiffDocument Read(DatabaseDiff diff) => DiffReader.Default.Read(diff);

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
                new ColumnDiff("name", ChangeKind.Add, new Column("name", SqlType.VarChar(255), DefaultExpression: "'anonymous'"), null, null, null, null, null, null),
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
                new ColumnDiff("total_label", ChangeKind.Modify, Generated: new ValueChange<string>(null, "total::text")),
                new ColumnDiff("amount", ChangeKind.Add, new Column("amount", SqlType.Int, GeneratedExpression: "total * 100"), null, null, null, null, null, null),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, new Column("legacy_flag", SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Remove, "writers", TablePrivilege.Insert)],
            Indexes: [],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Modify, "orders_pkey", null, new ValueChange<string>("old note", "new note"))],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, "orders_code_uq", null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Remove, "orders_total_chk", null)],
            ExclusionConstraints:
            [
                new ExclusionConstraintDiff(ChangeKind.Add, "orders_slot_excl", new ExclusionConstraint("orders_slot_excl", [new ExclusionElement("slot", "&&")], "gist")),
                new ExclusionConstraintDiff(ChangeKind.Remove, "orders_old_excl", null),
            ]);

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
                    // Materialized views: an add (with index on the definition) and an in-place index change.
                    new ViewDiff("app", "mv_sales", ChangeKind.Add,
                        Definition: new View("mv_sales", "SELECT date, sum(amount) FROM app.sales GROUP BY date", IsMaterialized: true),
                        Comment: new ValueChange<string>(null, "sales rollup"), IsMaterialized: true),
                    new ViewDiff("app", "mv_active", ChangeKind.Modify, IsMaterialized: true,
                        Indexes:
                        [
                            new IndexDiff(ChangeKind.Add, "mv_active_ix", new TableIndex("mv_active_ix", ["id"])),
                            new IndexDiff(ChangeKind.Remove, "mv_active_old_ix"),
                        ]),
                    // A plain → materialized conversion (a recreate carrying the materialization flip).
                    new ViewDiff("app", "hourly_totals", ChangeKind.Modify,
                        Definition: new View("hourly_totals", "SELECT date_trunc('hour', at), sum(amount) FROM app.sales GROUP BY 1", IsMaterialized: true),
                        IsMaterialized: true, Materialized: new ValueChange<bool>(false, true), RequiresRecreate: true),
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
    public Task Read_RichDiff() => Verify(Read(RichDiff()));

    [Fact]
    public Task Read_ViewChanges() => Verify(Read(ViewChangesDiff()));

    /// <summary>
    /// A diff exercising every function change kind: an add (showing arguments), a body-only replace, a
    /// signature change (recreate), a rename, a comment-only change, and a removal — plus a procedure variant.
    /// </summary>
    private static DatabaseDiff RoutineChangesDiff()
    {
        var addTax = new Routine("add_tax", RoutineKind.Function, "amount numeric, rate numeric", "RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$");
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app",
                    Routines:
                    [
                        new RoutineDiff("app", "add_tax", ChangeKind.Add, RoutineKind.Function, Definition: addTax,
                            Comment: new ValueChange<string>(null, "adds tax")),
                        new RoutineDiff("app", "normalize", ChangeKind.Modify, RoutineKind.Function,
                            Definition: new Routine("normalize", RoutineKind.Function, "code text", "RETURNS text AS $$ SELECT lower(code) $$")),
                        new RoutineDiff("app", "score", ChangeKind.Modify, RoutineKind.Function,
                            Definition: new Routine("score", RoutineKind.Function, "user_id bigint, weight numeric", "RETURNS numeric AS $$ SELECT 1 $$"),
                            Arguments: new ValueChange<string>("user_id bigint", "user_id bigint, weight numeric")),
                        new RoutineDiff("app", "renamed_fn", ChangeKind.Modify, RoutineKind.Function, RenamedFrom: "old_fn"),
                        new RoutineDiff("app", "noted", ChangeKind.Modify, RoutineKind.Function, Comment: new ValueChange<string>("old note", "new note")),
                        new RoutineDiff("app", "stale_fn", ChangeKind.Remove, RoutineKind.Function),
                        new RoutineDiff("app", "archive", ChangeKind.Add, RoutineKind.Procedure,
                            Definition: new Routine("archive", RoutineKind.Procedure, "before date", "LANGUAGE sql AS $$ DELETE $$")),
                        new RoutineDiff("app", "cleanup", ChangeKind.Modify, RoutineKind.Procedure,
                            Definition: new Routine("cleanup", RoutineKind.Procedure, "", "LANGUAGE sql AS $$ TRUNCATE $$"),
                            Arguments: new ValueChange<string>("batch int", "")),
                        new RoutineDiff("app", "stale_proc", ChangeKind.Remove, RoutineKind.Procedure),
                    ]),
            ]);
    }

    /// <summary>
    /// A diff exercising every extension change kind: an add (showing version), a bare add, a version change, a
    /// comment-only change, and a removal — all at the root, since extensions are database-global.
    /// </summary>
    private static DatabaseDiff ExtensionChangesDiff()
    {
        return new DatabaseDiff(
            Extensions:
            [
                new ExtensionDiff("postgis", ChangeKind.Add, Definition: new Extension("postgis", "3.4"),
                    Comment: new ValueChange<string>(null, "spatial types")),
                new ExtensionDiff("citext", ChangeKind.Add, Definition: new Extension("citext")),
                new ExtensionDiff("vector", ChangeKind.Modify, Version: new ValueChange<string>("0.6.0", "0.7.0")),
                new ExtensionDiff("hstore", ChangeKind.Modify, Comment: new ValueChange<string>("old note", "new note")),
                new ExtensionDiff("legacy_ext", ChangeKind.Remove),
            ]);
    }

    /// <summary>
    /// A diff exercising trigger changes on a table: an add, a comment-only modify, and a removal.
    /// </summary>
    private static DatabaseDiff TriggerChangesDiff()
    {
        var audit = new Trigger("audit", TriggerTiming.After, TriggerEvent.Insert | TriggerEvent.Update, "app.log", TriggerLevel.Row);
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Tables:
                [
                    new TableDiff("app", "users", ChangeKind.Modify, Triggers:
                    [
                        new TriggerDiff(ChangeKind.Add, "audit", audit),
                        new TriggerDiff(ChangeKind.Modify, "noted", null, new ValueChange<string>("old note", "new note")),
                        new TriggerDiff(ChangeKind.Remove, "stale_trg"),
                    ]),
                ]),
            ]);
    }

    /// <summary>
    /// A diff exercising domain changes: an add, a base-type change (recreate), a default change, a not-null
    /// change, a check add + drop, a rename, a comment-only change, and a removal.
    /// </summary>
    private static DatabaseDiff DomainChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Domains:
                [
                    new DomainDiff("app", "typeid", ChangeKind.Add,
                        Definition: new Domain("typeid", SqlType.Text, NotNull: true),
                        Comment: new ValueChange<string>(null, "id as text")),
                    new DomainDiff("app", "code", ChangeKind.Modify,
                        Definition: new Domain("code", SqlType.VarChar(8)),
                        DataType: new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(8))),
                    new DomainDiff("app", "amount", ChangeKind.Modify,
                        Default: new ValueChange<string>(null, "0"),
                        NotNull: new ValueChange<bool>(false, true),
                        Checks: [new CheckConstraintDiff(ChangeKind.Add, "amount_pos", new CheckConstraint("amount_pos", "VALUE >= 0"))]),
                    new DomainDiff("app", "email", ChangeKind.Modify,
                        Checks: [new CheckConstraintDiff(ChangeKind.Remove, "email_fmt")]),
                    new DomainDiff("app", "renamed_d", ChangeKind.Modify, RenamedFrom: "old_d"),
                    new DomainDiff("app", "noted", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new")),
                    new DomainDiff("app", "stale_d", ChangeKind.Remove),
                ]),
            ]);
    }

    [Fact]
    public Task Read_EnumChanges() => Verify(Read(EnumChangesDiff()));

    private static DatabaseDiff CompositeTypeChangesDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", CompositeTypes:
                [
                    new CompositeTypeDiff("app", "address", ChangeKind.Add,
                        Definition: new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)]),
                        Comment: new ValueChange<string>(null, "a postal address")),
                    new CompositeTypeDiff("app", "money", ChangeKind.Modify, Fields:
                    [
                        new CompositeFieldDiff(ChangeKind.Add, "currency", new CompositeField("currency", SqlType.Text)),
                        new CompositeFieldDiff(ChangeKind.Modify, "amount", Type: new ValueChange<SqlType>(SqlType.Int, SqlType.Decimal(18, 2))),
                        new CompositeFieldDiff(ChangeKind.Remove, "legacy"),
                    ]),
                    new CompositeTypeDiff("app", "renamed_t", ChangeKind.Modify, RenamedFrom: "old_t"),
                    new CompositeTypeDiff("app", "noted", ChangeKind.Modify, Comment: new ValueChange<string>("old", "new")),
                    new CompositeTypeDiff("app", "stale_t", ChangeKind.Remove),
                ]),
            ]);
    }

    [Fact]
    public Task Read_DomainChanges() => Verify(Read(DomainChangesDiff()));

    [Fact]
    public Task Read_CompositeTypeChanges() => Verify(Read(CompositeTypeChangesDiff()));

    [Fact]
    public Task Read_ExtensionChanges() => Verify(Read(ExtensionChangesDiff()));

    [Fact]
    public Task Read_TriggerChanges() => Verify(Read(TriggerChangesDiff()));

    [Fact]
    public Task Read_RoutineChanges() => Verify(Read(RoutineChangesDiff()));

    [Fact]
    public Task Read_SequenceChanges() => Verify(Read(SequenceChangesDiff()));

    /// <summary>
    /// A diff whose changes carry matched data migrations: a required column add backed by a named backfill, a
    /// type change backed by an anonymous migration, and a unique-constraint add backed by a named de-dupe.
    /// </summary>
    private static DatabaseDiff DataMigrationAnnotationsDiff()
    {
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("app", Tables:
                [
                    new TableDiff("app", "users", ChangeKind.Modify,
                        Columns:
                        [
                            new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))
                            {
                                Migration = new DataMigration("backfill emails", DataMigrationTrigger.AddColumn,
                                    "app", "users", "email", "UPDATE app.users SET email = ''"),
                            },
                            new ColumnDiff("total", ChangeKind.Modify,
                                Type: new ValueChange<SqlType>(SqlType.Text, SqlType.Int))
                            {
                                Migration = new DataMigration(null, DataMigrationTrigger.AlterColumnType,
                                    "app", "users", "total", "UPDATE app.users SET total = '0' WHERE total !~ '^\\d+$'"),
                            },
                        ],
                        UniqueConstraints:
                        [
                            new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"]))
                            {
                                Migration = new DataMigration("dedupe emails", DataMigrationTrigger.AddConstraint,
                                    "app", "users", "users_email_uq", "DELETE FROM app.users WHERE id NOT IN (SELECT min(id) FROM app.users GROUP BY email)"),
                            },
                        ]),
                ]),
            ]);
    }

    [Fact]
    public Task Read_DataMigrationAnnotations() => Verify(Read(DataMigrationAnnotationsDiff()));

    [Fact]
    public Task Read_EmptyDiff() => Verify(Read(new DatabaseDiff([])));
}
