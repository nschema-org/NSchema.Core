using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.CompositeTypes;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Domains;
using NSchema.Diff.Model.Enums;
using NSchema.Diff.Model.Extensions;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Routines;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Sequences;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Triggers;
using NSchema.Diff.Model.Views;
using NSchema.Diff.Reader;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Scripts;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;

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
            Schema: new SqlIdentifier("app"), Name: new SqlIdentifier("users"), Kind: ChangeKind.Add, RenamedFrom: null,
            Comment: new ValueChange<string>(null, "all users"),
            Columns:
            [
                new ColumnDiff(new SqlIdentifier("id"), ChangeKind.Add, new Column(new SqlIdentifier("id"), SqlType.BigInt, isIdentity: true,
                    identityOptions: new IdentityOptions(1, 1, 1)), null, null, null, null, null, null),
                new ColumnDiff(new SqlIdentifier("name"), ChangeKind.Add, new Column(new SqlIdentifier("name"), SqlType.VarChar(255), defaultExpression: new SqlText("'anonymous'")), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Add, new SqlIdentifier("readers"), TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, new SqlIdentifier("users_name_ix"), new TableIndex(new SqlIdentifier("users_name_ix"), ["name"], isUnique: true), null)],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pkey"), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), null)]);

        var modifiedTable = new TableDiff(
            Schema: new SqlIdentifier("app"), Name: new SqlIdentifier("orders"), Kind: ChangeKind.Modify, RenamedFrom: new SqlIdentifier("purchases"),
            Comment: null,
            Columns:
            [
                new ColumnDiff(new SqlIdentifier("total"), ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff(new SqlIdentifier("total_label"), ChangeKind.Modify, Generated: new ValueChange<SqlText>(null, new SqlText("total::text"))),
                new ColumnDiff(new SqlIdentifier("amount"), ChangeKind.Add, new Column(new SqlIdentifier("amount"), SqlType.Int, generatedExpression: new SqlText("total * 100")), null, null, null, null, null, null),
                new ColumnDiff(new SqlIdentifier("legacy_flag"), ChangeKind.Remove, new Column(new SqlIdentifier("legacy_flag"), SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [new GrantChange(ChangeKind.Remove, new SqlIdentifier("writers"), TablePrivilege.Insert)],
            Indexes: [],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Modify, new SqlIdentifier("orders_pkey"), null, new ValueChange<string>("old note", "new note"))],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, new SqlIdentifier("orders_user_fk"), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, new SqlIdentifier("orders_code_uq"), null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Remove, new SqlIdentifier("orders_total_chk"), null)],
            ExclusionConstraints:
            [
                new ExclusionConstraintDiff(ChangeKind.Add, new SqlIdentifier("orders_slot_excl"), new ExclusionConstraint(new SqlIdentifier("orders_slot_excl"), [new ExclusionElement("&&", new SqlIdentifier("slot"))], "gist")),
                new ExclusionConstraintDiff(ChangeKind.Remove, new SqlIdentifier("orders_old_excl"), null),
            ]);

        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff(new SqlIdentifier("reporting"), ChangeKind.Add, null,
                    Comment: new ValueChange<string>(null, "analytics"),
                    Grants: [new GrantChange(ChangeKind.Add, new SqlIdentifier("analyst"), null)],
                    Tables: []),
                new SchemaDiff(new SqlIdentifier("app"), null, null, null, [], [addedTable, modifiedTable]),
                new SchemaDiff(new SqlIdentifier("scratch"), ChangeKind.Remove, null, null, [], []),
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
                new SchemaDiff(new SqlIdentifier("app"), Views:
                [
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Add,
                        Definition: new View(new SqlIdentifier("active_users"), new SqlText("SELECT id FROM app.users WHERE active")),
                        Comment: new ValueChange<string>(null, "currently active users")),
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("daily_totals"), ChangeKind.Modify,
                        Definition: new View(new SqlIdentifier("daily_totals"), new SqlText("SELECT date, sum(amount) FROM app.sales GROUP BY date"))),
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("summary"), ChangeKind.Modify,
                        Comment: new ValueChange<string>("old summary", "new summary")),
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("report"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("legacy_report")),
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_view"), ChangeKind.Remove),
                    // Materialized views: an add (with index on the definition) and an in-place index change.
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("mv_sales"), ChangeKind.Add,
                        Definition: new View(new SqlIdentifier("mv_sales"), new SqlText("SELECT date, sum(amount) FROM app.sales GROUP BY date"), isMaterialized: true),
                        Comment: new ValueChange<string>(null, "sales rollup"), IsMaterialized: true),
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("mv_active"), ChangeKind.Modify, IsMaterialized: true,
                        Indexes:
                        [
                            new IndexDiff(ChangeKind.Add, new SqlIdentifier("mv_active_ix"), new TableIndex(new SqlIdentifier("mv_active_ix"), ["id"])),
                            new IndexDiff(ChangeKind.Remove, new SqlIdentifier("mv_active_old_ix")),
                        ]),
                    // A plain → materialized conversion (a recreate carrying the materialization flip).
                    new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("hourly_totals"), ChangeKind.Modify,
                        Definition: new View(new SqlIdentifier("hourly_totals"), new SqlText("SELECT date_trunc('hour', at), sum(amount) FROM app.sales GROUP BY 1"), isMaterialized: true),
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
                new SchemaDiff(new SqlIdentifier("app"), Enums:
                [
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("order_status"), ChangeKind.Add,
                        Definition: new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped", "delivered"]),
                        Comment: new ValueChange<string>(null, "order lifecycle")),
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("priority"), ChangeKind.Modify,
                        AddedValues:
                        [
                            new EnumValueAddition("lowest", Before: "low"),
                            new EnumValueAddition("medium", After: "low"),
                            new EnumValueAddition("highest"),
                        ],
                        Values: new ValueChange<IReadOnlyList<string>>(["low", "high"], ["lowest", "low", "medium", "high", "highest"])),
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("severity"), ChangeKind.Modify,
                        Values: new ValueChange<IReadOnlyList<string>>(["info", "warn", "error"], ["warn", "error"])),
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("kind"), ChangeKind.Modify,
                        Comment: new ValueChange<string>("old note", "new note")),
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("status"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("state")),
                    new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_enum"), ChangeKind.Remove),
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
                new SchemaDiff(new SqlIdentifier("app"), Sequences:
                [
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("order_id"), ChangeKind.Add,
                        Definition: new Sequence(new SqlIdentifier("order_id"),
                            new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MaxValue: 999999, Cache: 10, Cycle: true)),
                        Comment: new ValueChange<string>(null, "order numbers")),
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("invoice_id"), ChangeKind.Add,
                        Definition: new Sequence(new SqlIdentifier("invoice_id"))),
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("ticket_id"), ChangeKind.Modify,
                        Options: new ValueChange<SequenceOptions>(
                            new SequenceOptions(StartWith: 1, IncrementBy: 1),
                            new SequenceOptions(StartWith: 1000, IncrementBy: 10, Cycle: true))),
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("audit_id"), ChangeKind.Modify,
                        Comment: new ValueChange<string>("old note", "new note")),
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("batch_id"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("job_id")),
                    new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_seq"), ChangeKind.Remove),
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
        var addTax = new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric, rate numeric"), new SqlText("RETURNS numeric LANGUAGE sql AS $$ SELECT amount $$"));
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff(new SqlIdentifier("app"),
                    Routines:
                    [
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("add_tax"), ChangeKind.Add, RoutineKind.Function, Definition: addTax,
                            Comment: new ValueChange<string>(null, "adds tax")),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("normalize"), ChangeKind.Modify, RoutineKind.Function,
                            Definition: new Routine(new SqlIdentifier("normalize"), RoutineKind.Function, new SqlText("code text"), new SqlText("RETURNS text AS $$ SELECT lower(code) $$"))),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("score"), ChangeKind.Modify, RoutineKind.Function,
                            Definition: new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint, weight numeric"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                            Arguments: new ValueChange<SqlText>(new SqlText("user_id bigint"), new SqlText("user_id bigint, weight numeric"))),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("renamed_fn"), ChangeKind.Modify, RoutineKind.Function, RenamedFrom: new SqlIdentifier("old_fn")),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("noted"), ChangeKind.Modify, RoutineKind.Function, Comment: new ValueChange<string>("old note", "new note")),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_fn"), ChangeKind.Remove, RoutineKind.Function),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("archive"), ChangeKind.Add, RoutineKind.Procedure,
                            Definition: new Routine(new SqlIdentifier("archive"), RoutineKind.Procedure, new SqlText("before date"), new SqlText("LANGUAGE sql AS $$ DELETE $$"))),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("cleanup"), ChangeKind.Modify, RoutineKind.Procedure,
                            Definition: new Routine(new SqlIdentifier("cleanup"), RoutineKind.Procedure, new SqlText(""), new SqlText("LANGUAGE sql AS $$ TRUNCATE $$")),
                            Arguments: new ValueChange<SqlText>(new SqlText("batch int"), new SqlText(""))),
                        new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_proc"), ChangeKind.Remove, RoutineKind.Procedure),
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
                new ExtensionDiff(new SqlIdentifier("postgis"), ChangeKind.Add, Definition: new Extension(new SqlIdentifier("postgis"), "3.4"),
                    Comment: new ValueChange<string>(null, "spatial types")),
                new ExtensionDiff(new SqlIdentifier("citext"), ChangeKind.Add, Definition: new Extension(new SqlIdentifier("citext"))),
                new ExtensionDiff(new SqlIdentifier("vector"), ChangeKind.Modify, Version: new ValueChange<string>("0.6.0", "0.7.0")),
                new ExtensionDiff(new SqlIdentifier("hstore"), ChangeKind.Modify, Comment: new ValueChange<string>("old note", "new note")),
                new ExtensionDiff(new SqlIdentifier("legacy_ext"), ChangeKind.Remove),
            ]);
    }

    /// <summary>
    /// A diff exercising trigger changes on a table: an add, a comment-only modify, and a removal.
    /// </summary>
    private static DatabaseDiff TriggerChangesDiff()
    {
        var audit = new Trigger(new SqlIdentifier("audit"), TriggerTiming.After, TriggerEvent.Insert | TriggerEvent.Update, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log")), TriggerLevel.Row);
        return new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff(new SqlIdentifier("app"), Tables:
                [
                    new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Triggers:
                    [
                        new TriggerDiff(ChangeKind.Add, new SqlIdentifier("audit"), audit),
                        new TriggerDiff(ChangeKind.Modify, new SqlIdentifier("noted"), null, new ValueChange<string>("old note", "new note")),
                        new TriggerDiff(ChangeKind.Remove, new SqlIdentifier("stale_trg")),
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
                new SchemaDiff(new SqlIdentifier("app"), Domains:
                [
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("typeid"), ChangeKind.Add,
                        Definition: new DomainType(new SqlIdentifier("typeid"), SqlType.Text, notNull: true),
                        Comment: new ValueChange<string>(null, "id as text")),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("code"), ChangeKind.Modify,
                        Definition: new DomainType(new SqlIdentifier("code"), SqlType.VarChar(8)),
                        DataType: new ValueChange<SqlType>(SqlType.Text, SqlType.VarChar(8))),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("amount"), ChangeKind.Modify,
                        Default: new ValueChange<SqlText>(null, new SqlText("0")),
                        NotNull: new ValueChange<bool>(false, true),
                        Checks: [new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("amount_pos"), new CheckConstraint(new SqlIdentifier("amount_pos"), new SqlText("VALUE >= 0")))]),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("email"), ChangeKind.Modify,
                        Checks: [new CheckConstraintDiff(ChangeKind.Remove, new SqlIdentifier("email_fmt"))]),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("renamed_d"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("old_d")),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("noted"), ChangeKind.Modify, Comment: new ValueChange<string>("old", "new")),
                    new DomainDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_d"), ChangeKind.Remove),
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
                new SchemaDiff(new SqlIdentifier("app"), CompositeTypes:
                [
                    new CompositeTypeDiff(new SqlIdentifier("app"), new SqlIdentifier("address"), ChangeKind.Add,
                        Definition: new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int)]),
                        Comment: new ValueChange<string>(null, "a postal address")),
                    new CompositeTypeDiff(new SqlIdentifier("app"), new SqlIdentifier("money"), ChangeKind.Modify, Fields:
                    [
                        new CompositeFieldDiff(ChangeKind.Add, new SqlIdentifier("currency"), new CompositeField(new SqlIdentifier("currency"), SqlType.Text)),
                        new CompositeFieldDiff(ChangeKind.Modify, new SqlIdentifier("amount"), Type: new ValueChange<SqlType>(SqlType.Int, SqlType.Decimal(18, 2))),
                        new CompositeFieldDiff(ChangeKind.Remove, new SqlIdentifier("legacy")),
                    ]),
                    new CompositeTypeDiff(new SqlIdentifier("app"), new SqlIdentifier("renamed_t"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("old_t")),
                    new CompositeTypeDiff(new SqlIdentifier("app"), new SqlIdentifier("noted"), ChangeKind.Modify, Comment: new ValueChange<string>("old", "new")),
                    new CompositeTypeDiff(new SqlIdentifier("app"), new SqlIdentifier("stale_t"), ChangeKind.Remove),
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
        var backfill = ChangeScript("backfill_emails", ChangeTrigger.AddColumn, "email");
        var retype = ChangeScript("retype_totals", ChangeTrigger.AlterColumnType, "total");
        var dedupe = ChangeScript("dedupe_emails", ChangeTrigger.AddConstraint, "users_email_uq");
        var email = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = backfill };
        var total = new ColumnDiff(new SqlIdentifier("total"), ChangeKind.Modify, Type: new ValueChange<SqlType>(SqlType.Text, SqlType.Int)) { MigrationScript = retype };
        var uq = new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])) { MigrationScript = dedupe };
        var table = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [email, total], UniqueConstraints: [uq]);
        return new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [table])]);
    }

    private static ChangeScript ChangeScript(string name, ChangeTrigger trigger, string member) =>
        new(new SqlIdentifier(name), new SqlText($"-- {name}"), new SqlIdentifier("app"),
            trigger, new SqlIdentifier("users"), new SqlIdentifier(member));

    [Fact]
    public Task Read_DataMigrationAnnotations() => Verify(Read(DataMigrationAnnotationsDiff()));

    /// <summary>
    /// A diff whose root scripts list carries every event kind: deployment bookends and a matched change event.
    /// </summary>
    private static DatabaseDiff ScriptsDiff()
    {
        var backfill = new ChangeScript(new SqlIdentifier("backfill_emails"), new SqlText("UPDATE app.users SET email = '';"),
            new SqlIdentifier("app"), ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email"));
        var email = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = backfill };
        var table = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [email]);
        return new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [table])])
        {
            DeploymentScripts =
            [
                new DeploymentScript(new SqlIdentifier("seed_roles"), new SqlText("INSERT INTO roles VALUES ('admin');"), null, DeploymentPhase.Pre),
                new DeploymentScript(new SqlIdentifier("refresh_views"), new SqlText("REFRESH MATERIALIZED VIEW app.stats;"), null, DeploymentPhase.Post) { RunCondition = RunCondition.Once },
            ],
        };
    }

    [Fact]
    public Task Read_Scripts() => Verify(Read(ScriptsDiff()));

    [Fact]
    public Task Read_EmptyDiff() => Verify(Read(new DatabaseDiff([])));
}
