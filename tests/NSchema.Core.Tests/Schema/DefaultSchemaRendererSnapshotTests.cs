using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Schema;

/// <summary>
/// Snapshot coverage for <see cref="DefaultSchemaRenderer"/>.
/// </summary>
public sealed class DefaultSchemaRendererSnapshotTests
{
    private static string Render(DatabaseSchema schema) => new DefaultSchemaRenderer().Render(schema);

    /// <summary>Builds a view with dependencies derived from its body, exactly as the DSL parser would.</summary>
    private static View View(string name, string body, string? comment = null) =>
        new(name, body, null, comment, ViewDependencyExtractor.Extract(body, "app"));

    /// <summary>
    /// A schema exercising schema comments and grants, identity/default/nullable/commented columns,
    /// a primary key, a foreign key, unique and partial indexes, table grants, and views (including a
    /// view that reads another view).
    /// </summary>
    private static DatabaseSchema RichSchema()
    {
        var users = new Table(
            "users",
            Comment: "all users",
            PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
            Columns:
            [
                new Column("id", SqlType.BigInt, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1)),
                new Column("email", SqlType.VarChar(255), Comment: "contact address"),
                new Column("status", SqlType.Text, IsNullable: true, DefaultExpression: "'active'"),
            ],
            Indexes:
            [
                new TableIndex("users_email_ix", ["email"], IsUnique: true),
                new TableIndex("users_active_ix", ["status"], Predicate: "status = 'active'"),
            ],
            Grants: [new TableGrant("readers", TablePrivilege.Select | TablePrivilege.Insert)]);

        var orders = new Table(
            "orders",
            Columns: [new Column("id", SqlType.BigInt), new Column("user_id", SqlType.BigInt)],
            ForeignKeys:
            [
                new ForeignKey("orders_user_fk", ["user_id"], "app", "users", ["id"]),
            ]);

        return new DatabaseSchema(
        [
            new SchemaDefinition(
                "app",
                Comment: "application schema",
                Tables: [users, orders],
                Grants: [new SchemaGrant("readers")],
                Views:
                [
                    View("active_users", "SELECT id, email FROM app.users WHERE status = 'active'", comment: "currently active users"),
                    View("user_orders", "SELECT u.email, o.id FROM app.active_users u JOIN app.orders o ON o.user_id = u.id"),
                ]),
        ]);
    }

    [Fact]
    public Task Render_RichSchema() => Verify(Render(RichSchema()));

    [Fact]
    public Task Render_EmptySchema() => Verify(Render(new DatabaseSchema([])));
}
