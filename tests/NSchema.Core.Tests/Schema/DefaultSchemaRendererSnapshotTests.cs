using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

/// <summary>
/// Snapshot coverage for <see cref="DefaultSchemaRenderer"/>.
/// </summary>
public sealed class DefaultSchemaRendererSnapshotTests
{
    private static string Render(DatabaseSchema schema) => new DefaultSchemaRenderer().Render(schema);

    /// <summary>
    /// A schema exercising schema comments and grants, identity/default/nullable/commented columns,
    /// a primary key, a foreign key, unique and partial indexes, and table grants.
    /// </summary>
    private static DatabaseSchema RichSchema()
    {
        var users = Table.Create(
            "users",
            comment: "all users",
            primaryKey: new PrimaryKey("users_pkey", ["id"]),
            columns:
            [
                Column.Create("id", SqlType.BigInt, isIdentity: true, identityOptions: new IdentityOptions(1, 1, 1)),
                Column.Create("email", SqlType.VarChar(255), comment: "contact address"),
                Column.Create("status", SqlType.Text, isNullable: true, defaultExpression: "'active'"),
            ],
            indexes:
            [
                TableIndex.Create("users_email_ix", ["email"], isUnique: true),
                TableIndex.Create("users_active_ix", ["status"], predicate: "status = 'active'"),
            ],
            grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)]);

        var orders = Table.Create(
            "orders",
            columns: [Column.Create("id", SqlType.BigInt), Column.Create("user_id", SqlType.BigInt)],
            foreignKeys:
            [
                ForeignKey.Create("orders_user_fk", ["user_id"], "app", "users", ["id"]),
            ]);

        return DatabaseSchema.Create(
        [
            SchemaDefinition.Create(
                "app",
                comment: "application schema",
                tables: [users, orders],
                grants: [new SchemaGrant("readers")]),
        ]);
    }

    [Fact]
    public Task Render_RichSchema() => Verify(Render(RichSchema()));

    [Fact]
    public Task Render_EmptySchema() => Verify(Render(DatabaseSchema.Create([])));
}
