using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

public sealed class DefaultSchemaRendererTests
{
    private readonly DefaultSchemaRenderer _sut = new();

    [Fact]
    public void Render_EmptySchema_ReportsEmpty()
    {
        _sut.Render(DatabaseSchema.Create([])).ShouldBe("Schema is empty.");
    }

    [Fact]
    public void Render_RendersSchemaTableAndColumns()
    {
        var users = Table.Create(
            "users",
            primaryKey: new PrimaryKey("users_pkey", ["id"]),
            columns:
            [
                Column.Create("id", SqlType.Int),
                Column.Create("email", SqlType.Text, isNullable: true),
            ]);
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [users])]);

        var output = _sut.Render(schema);

        output.ShouldContain("schema app");
        output.ShouldContain("table users");
        output.ShouldContain("id int not null");
        output.ShouldContain("email text null");
        output.ShouldContain("primary key users_pkey (id)");
    }

    [Fact]
    public void Render_RendersUniqueAndCheckConstraints()
    {
        var users = Table.Create(
            "users",
            columns: [Column.Create("email", SqlType.Text), Column.Create("age", SqlType.Int)],
            uniqueConstraints: [new UniqueConstraint("users_email_uq", ["email"])],
            checkConstraints: [new CheckConstraint("users_age_chk", "age >= 0")]);
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app", tables: [users])]);

        var output = _sut.Render(schema);

        output.ShouldContain("unique users_email_uq (email)");
        output.ShouldContain("check users_age_chk (age >= 0)");
    }
}
