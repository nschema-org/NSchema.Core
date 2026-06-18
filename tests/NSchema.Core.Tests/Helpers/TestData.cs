using NSchema.Diff.Model;
using NSchema.Plan.Model;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;

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
                        Indexes:
                        [
                            new TableIndex("users_name_ix", ["name"], IsUnique: true,
                                Comment: "unique names", Predicate: "name IS NOT NULL"),
                        ],
                        Grants: [new TableGrant("readers", TablePrivilege.All)]),
                ],
                DroppedTables: ["old_table"],
                Grants: [new SchemaGrant("app_role")],
                Views:
                [
                    View("active_users", "SELECT id, name FROM app.users WHERE balance > 0", comment: "currently active users"),
                    View("user_directory", "SELECT name FROM app.active_users", oldName: "legacy_directory"),
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
                Functions:
                [
                    new Function("add_tax", "amount numeric, rate numeric",
                        "RETURNS numeric LANGUAGE sql AS $$\n  SELECT amount * (1 + rate);\n$$",
                        Comment: "adds tax"),
                    new Function("normalize_code", "code text DEFAULT 'N/A'",
                        "RETURNS text LANGUAGE sql AS $body$ SELECT upper(code || ';suffix'); $body$",
                        OldName: "clean_code"),
                ],
                DroppedFunctions: ["stale_fn"],
                Procedures:
                [
                    new Procedure("archive_users", "",
                        "LANGUAGE sql AS $$\n  DELETE FROM app.users WHERE name <> 'a;b';\n$$",
                        Comment: "archival job"),
                ],
                DroppedProcedures: ["stale_proc"]),
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
}
