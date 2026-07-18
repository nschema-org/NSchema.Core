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
    public static Database RichSchema() => new Database
    {
        Schemas = [
            new Schema {
                Name = new SqlIdentifier("app"),

                Tables = [
                    new Table {
                        Name = new SqlIdentifier("users"),
                        PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pkey"), ColumnNames = [new SqlIdentifier("id")], Comment = "surrogate key" },

                        Columns = [
                            new Column { Name = new SqlIdentifier("id"), Type = SqlType.BigInt, IsIdentity = true,
                                IdentityOptions = new IdentityOptions(1, 1, 1) },
                            new Column { Name = new SqlIdentifier("name"), Type = SqlType.VarChar(255), Comment = "display name" },
                            new Column { Name = new SqlIdentifier("balance"), Type = SqlType.Decimal(18, 2), IsNullable = true, DefaultExpression = new SqlText("0") },
                            new Column { Name = new SqlIdentifier("code"), Type = SqlType.Char(8) },
                            new Column { Name = new SqlIdentifier("metadata"), Type = SqlType.Custom("jsonb"), IsNullable = true },
                            new Column { Name = new SqlIdentifier("name_upper"), Type = SqlType.Text, IsNullable = true, GeneratedExpression = new SqlText("upper(name)") },
                        ],
                        ForeignKeys = [
                            new ForeignKey { Name = new SqlIdentifier("users_org_fk"), ColumnNames = [new SqlIdentifier("org_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("orgs"), ReferencedColumnNames = [new SqlIdentifier("id")],
                                OnDelete = ReferentialAction.Cascade, OnUpdate = ReferentialAction.SetNull, Comment = "owning org" },
                        ],
                        UniqueConstraints = [
                            new UniqueConstraint { Name = new SqlIdentifier("users_code_uq"), ColumnNames = [new SqlIdentifier("code")], Comment = "external code" },
                        ],
                        CheckConstraints = [
                            new CheckConstraint { Name = new SqlIdentifier("users_balance_chk"), Expression = new SqlText("balance >= 0"), Comment = "no overdraft" },
                        ],
                        ExclusionConstraints = [
                            new ExclusionConstraint { Name = new SqlIdentifier("users_code_excl"),
                                Elements = [new ExclusionElement("=", new SqlIdentifier("code")), new ExclusionElement("&&", Expression: new SqlText("int4range(0, balance)"))],
                                Method = "gist", Predicate = new SqlText("balance > 0"), Comment = "no overlap" },
                        ],
                        Indexes = [
                            new TableIndex { Name = new SqlIdentifier("users_name_ix"), Columns = ["name"], IsUnique = true,
                                Predicate = new SqlText("name IS NOT NULL"), Comment = "unique names" },
                            new TableIndex { Name = new SqlIdentifier("users_balance_ix"),
                                Columns = [new IndexColumn(new SqlIdentifier("balance"), Sort: IndexSort.Descending, Nulls: IndexNulls.Last), new IndexColumn(Expression: new SqlText("lower(name)"))],
                                Method = "btree", Include = [new SqlIdentifier("code")], Comment = "covering balance index" },
                        ],
                        Grants = [new TableGrant(new SqlIdentifier("readers"), TablePrivilege.All)],
                        Triggers = [
                            new Trigger { Name = new SqlIdentifier("users_audit"), Timing = TriggerTiming.After,
                                Events = TriggerEvent.Insert | TriggerEvent.Update, Function = new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("log_change")),
                                Level = TriggerLevel.Row, UpdateOfColumns = [new SqlIdentifier("name"), new SqlIdentifier("balance")],
                                When = new SqlText("new.balance > 0"), Comment = "audit row changes" },
                            new Trigger { Name = new SqlIdentifier("users_stamp"), Timing = TriggerTiming.Before, Events = TriggerEvent.Update,
                                Function = new RoutineReference(new SqlIdentifier("app"), new SqlIdentifier("touch_updated_at")) },
                            // An inline-body (SQL Server-style) trigger: no function, a multi-statement body that
                            // carries its own ';' (so it exercises the dollar-quoted round-trip).
                            new Trigger { Name = new SqlIdentifier("users_guard"), Timing = TriggerTiming.InsteadOf, Events = TriggerEvent.Delete,
                                Body = new SqlText("BEGIN\n  INSERT INTO app.audit (msg) VALUES ('blocked');\n  RETURN;\nEND"), Comment = "block deletes" },
                        ], Comment = "all users" },
                ],
                Grants = [new SchemaGrant(new SqlIdentifier("app_role"))],
                Views = [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users"),
                    MaterializedView("daily_balances", "SELECT name, balance FROM app.users",
                        comment: "balances rollup",
                        indexes: [new TableIndex { Name = new SqlIdentifier("daily_balances_name_ix"), Columns = ["name"], IsUnique = true, Comment = "by name" }]),
                ],
                Enums = [
                    new EnumType { Name = new SqlIdentifier("order_status"), Values = ["pending", "shipped", "delivered"], Comment = "order lifecycle" },
                    new EnumType { Name = new SqlIdentifier("priority"), Values = ["low", "high"] },
                ],
                Sequences = [
                    new Sequence { Name = new SqlIdentifier("invoice_id") },
                    new Sequence { Name = new SqlIdentifier("order_id"),
                        Options = new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: -10, MaxValue: 999999, Cache: 10, Cycle: true), Comment = "order numbers" },
                ],
                Routines = [
                    new Routine { Name = new SqlIdentifier("add_tax"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("amount numeric, rate numeric"),
                        Definition = new SqlText("RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$"), Comment = "adds tax" },
                    new Routine { Name = new SqlIdentifier("normalize_code"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("code text DEFAULT 'N/A'"),
                        Definition = new SqlText("RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$") },
                    new Routine { Name = new SqlIdentifier("archive_users"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText(""),
                        Definition = new SqlText("LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$"), Comment = "archival job" },
                ],
                Domains = [
                    new DomainType { Name = new SqlIdentifier("typeid"), DataType = SqlType.Text, Comment = "unique id as text" },
                    new DomainType { Name = new SqlIdentifier("positive_amount"), DataType = SqlType.Decimal(18, 2), Default = new SqlText("0"), NotNull = true,
                        Checks = [new CheckConstraint { Name = new SqlIdentifier("positive_amount_chk"), Expression = new SqlText("VALUE >= 0") }] },
                ],
                CompositeTypes = [
                    new CompositeType { Name = new SqlIdentifier("address"), Fields = [new CompositeField(new SqlIdentifier("street"), SqlType.Text), new CompositeField(new SqlIdentifier("zip"), SqlType.Int)], Comment = "a postal address" },
                    new CompositeType { Name = new SqlIdentifier("money_amount"), Fields = [new CompositeField(new SqlIdentifier("amount"), SqlType.Decimal(18, 2)), new CompositeField(new SqlIdentifier("currency"), SqlType.Text)] },
                ], Comment = "application schema" },
        ],
        Extensions = [
            new Extension { Name = new SqlIdentifier("citext") },
            new Extension { Name = new SqlIdentifier("postgis"), Version = "3.4", Comment = "spatial types" },
            new Extension { Name = new SqlIdentifier("uuid-ossp"), Comment = "uuid generation" },
        ],
    };

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
        new View { Name = new SqlIdentifier(name), Body = new SqlText(body), DependsOn = ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")), Comment = comment };

    /// <summary>Builds a materialized view (optionally with indexes), dependencies derived from its body.</summary>
    private static View MaterializedView(string name, string body, string? comment = null, DatabaseMemberCollection<TableIndex>? indexes = null) =>
        new View { Name = new SqlIdentifier(name), Body = new SqlText(body), DependsOn = ViewDependencyExtractor.Extract(body, new SqlIdentifier("app")), IsMaterialized = true, Indexes = indexes ?? [], Comment = comment };
}
