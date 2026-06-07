using NSchema.Diff.Model;
using NSchema.Plan.Model;
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
    public static DatabaseSchema RichSchema() => DatabaseSchema.Create(
        schemas:
        [
            SchemaDefinition.Create(
                name: "app",
                oldName: "legacy_app",
                isPartial: true,
                comment: "application schema",
                tables:
                [
                    Table.Create(
                        name: "users",
                        oldName: "members",
                        primaryKey: new PrimaryKey("users_pkey", ["id"]),
                        comment: "all users",
                        columns:
                        [
                            Column.Create("id", SqlType.BigInt, isIdentity: true,
                                identityOptions: new IdentityOptions(1, 1, 1)),
                            Column.Create("name", SqlType.VarChar(255), comment: "display name"),
                            Column.Create("balance", SqlType.Decimal(18, 2), isNullable: true, defaultExpression: "0"),
                            Column.Create("code", SqlType.Char(8), oldName: "short_code"),
                            Column.Create("metadata", SqlType.Custom("jsonb"), isNullable: true),
                        ],
                        foreignKeys:
                        [
                            ForeignKey.Create("users_org_fk", ["org_id"], "app", "orgs", ["id"],
                                ReferentialAction.Cascade, ReferentialAction.SetNull),
                        ],
                        indexes:
                        [
                            TableIndex.Create("users_name_ix", ["name"], isUnique: true,
                                comment: "unique names", predicate: "name IS NOT NULL"),
                        ],
                        grants: [new TableGrant("readers", TablePrivilege.All)]),
                ],
                droppedTables: ["old_table"],
                grants: [new SchemaGrant("app_role")]),
        ],
        droppedSchemas: ["scratch"]);
}
