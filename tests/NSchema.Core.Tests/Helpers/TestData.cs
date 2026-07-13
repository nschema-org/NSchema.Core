using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Plan.Domain.Models;
using NSchema.Plan.Domain.Models.Schemas;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Project.Ddl;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.CompositeTypes;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Domains;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Triggers;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Helpers;

public static class TestData
{
    public static readonly MigrationAction DestructiveAction = new DropTable(new SqlIdentifier("identity"), new SqlIdentifier("users"));
    public static readonly MigrationAction NonDestructiveAction = new CreateSchema(new SqlIdentifier("identity"));

    /// <summary>A diff dropping the <c>identity.users</c> table.</summary>
    public static readonly DatabaseDiff DestructiveDiff = DiffWithDroppedTables("users");

    /// <summary>A diff that only adds a schema.</summary>
    public static readonly DatabaseDiff NonDestructiveDiff = new(
        [new SchemaDiff(new SqlIdentifier("identity"), ChangeKind.Add, null, null, [], [])]);

    /// <summary>Builds a diff that drops the named tables from the <c>identity</c> schema.</summary>
    public static DatabaseDiff DiffWithDroppedTables(params string[] tableNames) => new(
        [new SchemaDiff(new SqlIdentifier("identity"), null, null, null, [],
            [.. tableNames.Select(name => new TableDiff(new SqlIdentifier("identity"), new SqlIdentifier(name), ChangeKind.Remove, null, null, [], [], [], []))])]);

    /// <summary>
    /// A schema exercising every domain feature (renames, identity, facets, comments, foreign keys,
    /// indexes, grants, dropped tables and schemas), for serializer round-trip and snapshot coverage.
    /// Shared so the state and document serializers are pinned against the same input.
    /// </summary>
    public static DatabaseSchema RichSchema() => new(
        Schemas:
        [
            new SchemaDefinition(
                Name: new SqlIdentifier("app"),
                OldName: new SqlIdentifier("legacy_app"),
                IsPartial: true,
                Comment: "application schema",
                Tables:
                [
                    new Table(
                        Name: new SqlIdentifier("users"),
                        OldName: new SqlIdentifier("members"),
                        PrimaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")], Comment: "surrogate key"),
                        Comment: "all users",
                        Columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.BigInt, IsIdentity: true,
                                IdentityOptions: new IdentityOptions(1, 1, 1)),
                            new Column(new SqlIdentifier("name"), SqlType.VarChar(255), Comment: "display name"),
                            new Column(new SqlIdentifier("balance"), SqlType.Decimal(18, 2), IsNullable: true, DefaultExpression: "0"),
                            new Column(new SqlIdentifier("code"), SqlType.Char(8), OldName: new SqlIdentifier("short_code")),
                            new Column(new SqlIdentifier("metadata"), SqlType.Custom("jsonb"), IsNullable: true),
                            new Column(new SqlIdentifier("name_upper"), SqlType.Text, IsNullable: true, GeneratedExpression: "upper(name)"),
                        ],
                        ForeignKeys:
                        [
                            new ForeignKey(new SqlIdentifier("users_org_fk"), [new SqlIdentifier("org_id")], new SqlIdentifier("app"), new SqlIdentifier("orgs"), [new SqlIdentifier("id")],
                                ReferentialAction.Cascade, ReferentialAction.SetNull, Comment: "owning org"),
                        ],
                        UniqueConstraints:
                        [
                            new UniqueConstraint(new SqlIdentifier("users_code_uq"), [new SqlIdentifier("code")], Comment: "external code"),
                        ],
                        CheckConstraints:
                        [
                            new CheckConstraint(new SqlIdentifier("users_balance_chk"), "balance >= 0", Comment: "no overdraft"),
                        ],
                        ExclusionConstraints:
                        [
                            new ExclusionConstraint(new SqlIdentifier("users_code_excl"),
                                [new ExclusionElement("code", "="), new ExclusionElement("int4range(0, balance)", "&&", IsExpression: true)],
                                Method: "gist", Predicate: "balance > 0", Comment: "no overlap"),
                        ],
                        Indexes:
                        [
                            new TableIndex(new SqlIdentifier("users_name_ix"), ["name"], IsUnique: true,
                                Comment: "unique names", Predicate: "name IS NOT NULL"),
                            new TableIndex(new SqlIdentifier("users_balance_ix"),
                                [new IndexColumn("balance", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn("lower(name)", IsExpression: true)],
                                Method: "btree", Include: [new SqlIdentifier("code")], Comment: "covering balance index"),
                        ],
                        Grants: [new TableGrant(new SqlIdentifier("readers"), TablePrivilege.All)],
                        Triggers:
                        [
                            new Trigger(new SqlIdentifier("users_audit"), TriggerTiming.After,
                                TriggerEvent.Insert | TriggerEvent.Update, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log_change")),
                                TriggerLevel.Row, UpdateOfColumns: [new SqlIdentifier("name"), new SqlIdentifier("balance")],
                                When: "new.balance > 0", Comment: "audit row changes"),
                            new Trigger(new SqlIdentifier("users_stamp"), TriggerTiming.Before, TriggerEvent.Update,
                                new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("touch_updated_at"))),
                            // An inline-body (SQL Server-style) trigger: no function, a multi-statement body that
                            // carries its own ';' (so it exercises the dollar-quoted round-trip).
                            new Trigger(new SqlIdentifier("users_guard"), TriggerTiming.InsteadOf, TriggerEvent.Delete,
                                Body: "BEGIN\n  INSERT INTO app.audit (msg) VALUES ('blocked');\n  RETURN;\nEND",
                                Comment: "block deletes"),
                        ]),
                ],
                DroppedTables: [new SqlIdentifier("old_table")],
                Grants: [new SchemaGrant(new SqlIdentifier("app_role"))],
                Views:
                [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users", oldName: new SqlIdentifier("legacy_directory")),
                    MaterializedView("daily_balances", "SELECT name, balance FROM app.users",
                        comment: "balances rollup",
                        indexes: [new TableIndex(new SqlIdentifier("daily_balances_name_ix"), ["name"], IsUnique: true, Comment: "by name")]),
                ],
                DroppedViews: [new SqlIdentifier("stale_report")],
                Enums:
                [
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped", "delivered"], Comment: "order lifecycle"),
                    new EnumType(new SqlIdentifier("priority"), ["low", "high"], OldName: new SqlIdentifier("importance")),
                ],
                DroppedEnums: [new SqlIdentifier("stale_enum")],
                Sequences:
                [
                    new Sequence(new SqlIdentifier("invoice_id"), OldName: new SqlIdentifier("bill_id")),
                    new Sequence(new SqlIdentifier("order_id"),
                        new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true),
                        Comment: "order numbers"),
                ],
                DroppedSequences: [new SqlIdentifier("stale_seq")],
                Routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, "amount numeric, rate numeric",
                        "RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$",
                        Comment: "adds tax"),
                    new Routine(new SqlIdentifier("normalize_code"), RoutineKind.Function, "code text DEFAULT 'N/A'",
                        "RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$",
                        OldName: new SqlIdentifier("clean_code")),
                    new Routine(new SqlIdentifier("archive_users"), RoutineKind.Procedure, "",
                        "LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$",
                        Comment: "archival job"),
                ],
                DroppedRoutines: [new SqlIdentifier("stale_fn"), new SqlIdentifier("stale_proc")],
                Domains:
                [
                    new DomainDefinition(new SqlIdentifier("typeid"), SqlType.Text, OldName: new SqlIdentifier("legacy_id"), Comment: "unique id as text"),
                    new DomainDefinition(new SqlIdentifier("positive_amount"), SqlType.Decimal(18, 2), Default: "0", NotNull: true,
                        Checks: [new CheckConstraint(new SqlIdentifier("positive_amount_chk"), "VALUE >= 0")]),
                ],
                DroppedDomains: [new SqlIdentifier("stale_domain")],
                CompositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int)],
                        OldName: new SqlIdentifier("legacy_address"), Comment: "a postal address"),
                    new CompositeType(new SqlIdentifier("money_amount"), [new CompositeField(new SqlIdentifier("amount"), SqlType.Decimal(18, 2)), new CompositeField(new SqlIdentifier("currency"), SqlType.Text)]),
                ],
                DroppedCompositeTypes: [new SqlIdentifier("stale_type")]),
        ],
        DroppedSchemas: [new SqlIdentifier("scratch")],
        Extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), Version: "3.4", Comment: "spatial types"),
            new Extension(new SqlIdentifier("uuid-ossp"), Comment: "uuid generation"),
        ],
        DroppedExtensions: [new SqlIdentifier("stale_ext")]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null, SqlIdentifier? oldName = null) =>
        new(new SqlIdentifier(name), body, oldName, comment, ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")));

    /// <summary>Builds a materialized view (optionally with indexes), dependencies derived from its body.</summary>
    private static View MaterializedView(string name, string body, string? comment = null, IReadOnlyList<TableIndex>? indexes = null) =>
        new(new SqlIdentifier(name), body, null, comment, ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")), IsMaterialized: true, Indexes: indexes);
}
