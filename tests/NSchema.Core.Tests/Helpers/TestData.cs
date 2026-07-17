using NSchema.Diff.Model;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Constraints;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Extensions;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Triggers;
using NSchema.Model.Views;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Plan.Model.Tables;
using NSchema.Project.Model.Directives;
using NSchema.Project.Nsql;

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
    /// A schema exercising every domain feature (identity, facets, comments, foreign keys,
    /// indexes, grants), for serializer round-trip and snapshot coverage.
    /// Shared so the state and document serializers are pinned against the same input.
    /// </summary>
    public static Database RichSchema() => new(
        schemas:
        [
            new Schema(
                name: new SqlIdentifier("app"),
                
                tables:
                [
                    new Table(
                        name: new SqlIdentifier("users"),
                        primaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]) { Comment = "surrogate key" },
                        
                        columns:
                        [
                            new Column(new SqlIdentifier("id"), SqlType.BigInt, isIdentity: true,
                                identityOptions: new IdentityOptions(1, 1, 1)),
                            new Column(new SqlIdentifier("name"), SqlType.VarChar(255)) { Comment = "display name" },
                            new Column(new SqlIdentifier("balance"), SqlType.Decimal(18, 2), isNullable: true, defaultExpression: new SqlText("0")),
                            new Column(new SqlIdentifier("code"), SqlType.Char(8)),
                            new Column(new SqlIdentifier("metadata"), SqlType.Custom("jsonb"), isNullable: true),
                            new Column(new SqlIdentifier("name_upper"), SqlType.Text, isNullable: true, generatedExpression: new SqlText("upper(name)")),
                        ],
                        foreignKeys:
                        [
                            new ForeignKey(new SqlIdentifier("users_org_fk"), [new SqlIdentifier("org_id")], new SqlIdentifier("app"), new SqlIdentifier("orgs"), [new SqlIdentifier("id")],
                                ReferentialAction.Cascade, ReferentialAction.SetNull) { Comment = "owning org" },
                        ],
                        uniqueConstraints:
                        [
                            new UniqueConstraint(new SqlIdentifier("users_code_uq"), [new SqlIdentifier("code")]) { Comment = "external code" },
                        ],
                        checkConstraints:
                        [
                            new CheckConstraint(new SqlIdentifier("users_balance_chk"), new SqlText("balance >= 0")) { Comment = "no overdraft" },
                        ],
                        exclusionConstraints:
                        [
                            new ExclusionConstraint(new SqlIdentifier("users_code_excl"),
                                [new ExclusionElement("=", new SqlIdentifier("code")), new ExclusionElement("&&", Expression: new SqlText("int4range(0, balance)"))],
                                method: "gist", predicate: new SqlText("balance > 0")) { Comment = "no overlap" },
                        ],
                        indexes:
                        [
                            new TableIndex(new SqlIdentifier("users_name_ix"), ["name"], isUnique: true,
                                predicate: new SqlText("name IS NOT NULL")) { Comment = "unique names" },
                            new TableIndex(new SqlIdentifier("users_balance_ix"),
                                [new IndexColumn(new SqlIdentifier("balance"), Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: new SqlText("lower(name)"))],
                                method: "btree", include: [new SqlIdentifier("code")]) { Comment = "covering balance index" },
                        ],
                        grants: [new TableGrant(new SqlIdentifier("readers"), TablePrivilege.All)],
                        triggers:
                        [
                            new Trigger(new SqlIdentifier("users_audit"), TriggerTiming.After,
                                TriggerEvent.Insert | TriggerEvent.Update, new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log_change")),
                                TriggerLevel.Row, updateOfColumns: [new SqlIdentifier("name"), new SqlIdentifier("balance")],
                                when: new SqlText("new.balance > 0")) { Comment = "audit row changes" },
                            new Trigger(new SqlIdentifier("users_stamp"), TriggerTiming.Before, TriggerEvent.Update,
                                new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("touch_updated_at"))),
                            // An inline-body (SQL Server-style) trigger: no function, a multi-statement body that
                            // carries its own ';' (so it exercises the dollar-quoted round-trip).
                            new Trigger(new SqlIdentifier("users_guard"), TriggerTiming.InsteadOf, TriggerEvent.Delete,
                                body: new SqlText("BEGIN\n  INSERT INTO app.audit (msg) VALUES ('blocked');\n  RETURN;\nEND")) { Comment = "block deletes" },
                        ]) { Comment = "all users" },
                ],
                grants: [new SchemaGrant(new SqlIdentifier("app_role"))],
                views:
                [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users"),
                    MaterializedView("daily_balances", "SELECT name, balance FROM app.users",
                        comment: "balances rollup",
                        indexes: [new TableIndex(new SqlIdentifier("daily_balances_name_ix"), ["name"], isUnique: true) { Comment = "by name" }]),
                ],
                enums:
                [
                    new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped", "delivered"]) { Comment = "order lifecycle" },
                    new EnumType(new SqlIdentifier("priority"), ["low", "high"]),
                ],
                sequences:
                [
                    new Sequence(new SqlIdentifier("invoice_id")),
                    new Sequence(new SqlIdentifier("order_id"),
                        new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true)) { Comment = "order numbers" },
                ],
                routines:
                [
                    new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric, rate numeric"),
                        new SqlText("RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$")) { Comment = "adds tax" },
                    new Routine(new SqlIdentifier("normalize_code"), RoutineKind.Function, new SqlText("code text DEFAULT 'N/A'"),
                        new SqlText("RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$")),
                    new Routine(new SqlIdentifier("archive_users"), RoutineKind.Procedure, new SqlText(""),
                        new SqlText("LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$")) { Comment = "archival job" },
                ],
                domains:
                [
                    new DomainType(new SqlIdentifier("typeid"), SqlType.Text) { Comment = "unique id as text" },
                    new DomainType(new SqlIdentifier("positive_amount"), SqlType.Decimal(18, 2), @default: new SqlText("0"), notNull: true,
                        checks: [new CheckConstraint(new SqlIdentifier("positive_amount_chk"), new SqlText("VALUE >= 0"))]),
                ],
                compositeTypes:
                [
                    new CompositeType(new SqlIdentifier("address"), [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int)]) { Comment = "a postal address" },
                    new CompositeType(new SqlIdentifier("money_amount"), [new CompositeField(new SqlIdentifier("amount"), SqlType.Decimal(18, 2)), new CompositeField(new SqlIdentifier("currency"), SqlType.Text)]),
                ]) { Comment = "application schema" },
        ],
        extensions:
        [
            new Extension(new SqlIdentifier("citext")),
            new Extension(new SqlIdentifier("postgis"), version: "3.4") { Comment = "spatial types" },
            new Extension(new SqlIdentifier("uuid-ossp")) { Comment = "uuid generation" },
        ]);

    /// <summary>
    /// Directives exercising every directive statement against <see cref="RichSchema"/>: a rename of every
    /// renameable kind (addressing current reality — the schema's current name is <c>legacy_app</c>).
    /// Shared so the writer round-trip pins the whole grammar.
    /// </summary>
    public static ProjectDirectives RichDirectives() => new(
        SchemaRenames: [new SchemaRenameDirective(new SqlIdentifier("legacy_app"), new SqlIdentifier("app"))],
        ObjectRenames:
        [
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Table, Current("members")), new SqlIdentifier("users")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.View, Current("legacy_directory")), new SqlIdentifier("user_directory")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Enum, Current("importance")), new SqlIdentifier("priority")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Sequence, Current("bill_id")), new SqlIdentifier("invoice_id")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Routine, Current("clean_code")), new SqlIdentifier("normalize_code")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.Domain, Current("legacy_id")), new SqlIdentifier("typeid")),
            new ObjectRenameDirective(new ObjectIdentity(ObjectKind.CompositeType, Current("legacy_address")), new SqlIdentifier("address")),
        ],
        MemberRenames: [new MemberRenameDirective(new MemberAddress(new SqlIdentifier("legacy_app"), new SqlIdentifier("members"), new SqlIdentifier("short_code")), new SqlIdentifier("code"))]);

    /// <summary>An address under the schema's current (pre-rename) name.</summary>
    private static ObjectAddress Current(string name) => new(new SqlIdentifier("legacy_app"), new SqlIdentifier(name));

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DDL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new(new SqlIdentifier(name), new SqlText(body), ViewDependencyExtractor.Extract(body, new SqlIdentifier("app"))) { Comment = comment };

    /// <summary>Builds a materialized view (optionally with indexes), dependencies derived from its body.</summary>
    private static View MaterializedView(string name, string body, string? comment = null, IReadOnlyList<TableIndex>? indexes = null) =>
        new(new SqlIdentifier(name), new SqlText(body), ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")), isMaterialized: true, indexes: indexes) { Comment = comment };
}
