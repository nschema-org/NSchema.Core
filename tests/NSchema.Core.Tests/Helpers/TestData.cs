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
    public static readonly MigrationAction DestructiveAction = new DropTable("identity", "users");
    public static readonly MigrationAction NonDestructiveAction = new CreateSchema("identity");

    /// <summary>A diff dropping the <c>identity.users</c> table.</summary>
    public static readonly DatabaseDiff DestructiveDiff = DiffWithDroppedTables("users");

    /// <summary>A diff that only adds a schema.</summary>
    public static readonly DatabaseDiff NonDestructiveDiff = new(
        [new SchemaDiff("identity", ChangeKind.Add, null, null, [], [])]);

    /// <summary>Builds a diff that drops the named tables from the <c>identity</c> schema.</summary>
    public static DatabaseDiff DiffWithDroppedTables(params string[] tableNames) => new(
        [new SchemaDiff("identity", null, null, null, [],
            [.. tableNames.Select(name => new TableDiff("identity", name, ChangeKind.Remove, null, null, [], [], [], []))])]);

    /// <summary>
    /// A schema exercising every domain feature (renames, identity, facets, comments, foreign keys,
    /// indexes, grants, dropped tables and schemas), for serializer round-trip and snapshot coverage.
    /// Shared so the state and document serializers are pinned against the same input.
    /// </summary>
    public static DatabaseSchema RichSchema() => new(
        Schemas:
        [
            new SchemaDefinition(
                Name: "app",
                OldName: "legacy_app",
                IsPartial: true,
                Comment: "application schema",
                Tables:
                [
                    new Table(
                        Name: "users",
                        OldName: "members",
                        PrimaryKey: new PrimaryKey("users_pkey", ["id"], Comment: "surrogate key"),
                        Comment: "all users",
                        Columns:
                        [
                            new Column("id", SqlType.BigInt, IsIdentity: true,
                                IdentityOptions: new IdentityOptions(1, 1, 1)),
                            new Column("name", SqlType.VarChar(255), Comment: "display name"),
                            new Column("balance", SqlType.Decimal(18, 2), IsNullable: true, DefaultExpression: "0"),
                            new Column("code", SqlType.Char(8), OldName: "short_code"),
                            new Column("metadata", SqlType.Custom("jsonb"), IsNullable: true),
                            new Column("name_upper", SqlType.Text, IsNullable: true, GeneratedExpression: "upper(name)"),
                        ],
                        ForeignKeys:
                        [
                            new ForeignKey("users_org_fk", ["org_id"], "app", "orgs", ["id"],
                                ReferentialAction.Cascade, ReferentialAction.SetNull, Comment: "owning org"),
                        ],
                        UniqueConstraints:
                        [
                            new UniqueConstraint("users_code_uq", ["code"], Comment: "external code"),
                        ],
                        CheckConstraints:
                        [
                            new CheckConstraint("users_balance_chk", "balance >= 0", Comment: "no overdraft"),
                        ],
                        ExclusionConstraints:
                        [
                            new ExclusionConstraint("users_code_excl",
                                [new ExclusionElement("code", "="), new ExclusionElement("int4range(0, balance)", "&&", IsExpression: true)],
                                Method: "gist", Predicate: "balance > 0", Comment: "no overlap"),
                        ],
                        Indexes:
                        [
                            new TableIndex("users_name_ix", ["name"], IsUnique: true,
                                Comment: "unique names", Predicate: "name IS NOT NULL"),
                            new TableIndex("users_balance_ix",
                                [new IndexColumn("balance", Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn("lower(name)", IsExpression: true)],
                                Method: "btree", Include: ["code"], Comment: "covering balance index"),
                        ],
                        Grants: [new TableGrant("readers", TablePrivilege.All)],
                        Triggers:
                        [
                            new Trigger("users_audit", TriggerTiming.After,
                                TriggerEvent.Insert | TriggerEvent.Update, "app.log_change",
                                TriggerLevel.Row, UpdateOfColumns: ["name", "balance"],
                                When: "new.balance > 0", Comment: "audit row changes"),
                            new Trigger("users_stamp", TriggerTiming.Before, TriggerEvent.Update,
                                "app.touch_updated_at"),
                            // An inline-body (SQL Server-style) trigger: no function, a multi-statement body that
                            // carries its own ';' (so it exercises the dollar-quoted round-trip).
                            new Trigger("users_guard", TriggerTiming.InsteadOf, TriggerEvent.Delete,
                                Body: "BEGIN\n  INSERT INTO app.audit (msg) VALUES ('blocked');\n  RETURN;\nEND",
                                Comment: "block deletes"),
                        ]),
                ],
                DroppedTables: ["old_table"],
                Grants: [new SchemaGrant("app_role")],
                Views:
                [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users", oldName: "legacy_directory"),
                    MaterializedView("daily_balances", "SELECT name, balance FROM app.users",
                        comment: "balances rollup",
                        indexes: [new TableIndex("daily_balances_name_ix", ["name"], IsUnique: true, Comment: "by name")]),
                ],
                DroppedViews: ["stale_report"],
                Enums:
                [
                    new EnumType("order_status", ["pending", "shipped", "delivered"], Comment: "order lifecycle"),
                    new EnumType("priority", ["low", "high"], OldName: "importance"),
                ],
                DroppedEnums: ["stale_enum"],
                Sequences:
                [
                    new Sequence("invoice_id", OldName: "bill_id"),
                    new Sequence("order_id",
                        new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true),
                        Comment: "order numbers"),
                ],
                DroppedSequences: ["stale_seq"],
                Routines:
                [
                    new Routine("add_tax", RoutineKind.Function, "amount numeric, rate numeric",
                        "RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$",
                        Comment: "adds tax"),
                    new Routine("normalize_code", RoutineKind.Function, "code text DEFAULT 'N/A'",
                        "RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$",
                        OldName: "clean_code"),
                    new Routine("archive_users", RoutineKind.Procedure, "",
                        "LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$",
                        Comment: "archival job"),
                ],
                DroppedRoutines: ["stale_fn", "stale_proc"],
                Domains:
                [
                    new DomainDefinition("typeid", SqlType.Text, OldName: "legacy_id", Comment: "unique id as text"),
                    new DomainDefinition("positive_amount", SqlType.Decimal(18, 2), Default: "0", NotNull: true,
                        Checks: [new CheckConstraint("positive_amount_chk", "VALUE >= 0")]),
                ],
                DroppedDomains: ["stale_domain"],
                CompositeTypes:
                [
                    new CompositeType("address", [new CompositeField("street", SqlType.Text), new CompositeField("zip", SqlType.Int)],
                        OldName: "legacy_address", Comment: "a postal address"),
                    new CompositeType("money_amount", [new CompositeField("amount", SqlType.Decimal(18, 2)), new CompositeField("currency", SqlType.Text)]),
                ],
                DroppedCompositeTypes: ["stale_type"]),
        ],
        DroppedSchemas: ["scratch"],
        Extensions:
        [
            new Extension("citext"),
            new Extension("postgis", Version: "3.4", Comment: "spatial types"),
            new Extension("uuid-ossp", Comment: "uuid generation"),
        ],
        DroppedExtensions: ["stale_ext"]);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null, string? oldName = null) =>
        new(name, body, oldName, comment, ViewDependencyExtractor.Extract(body, "app"));

    /// <summary>Builds a materialized view (optionally with indexes), dependencies derived from its body.</summary>
    private static View MaterializedView(string name, string body, string? comment = null, IReadOnlyList<TableIndex>? indexes = null) =>
        new(name, body, null, comment, ViewDependencyExtractor.Extract(body, "app"), IsMaterialized: true, Indexes: indexes);
}
