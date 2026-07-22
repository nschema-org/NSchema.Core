using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Project.Nsql;

namespace NSchema.Tests.Project.Serialization.Nsql;

/// <summary>
/// Quoted identifiers: quoting carries the characters a bare identifier cannot (and escapes the vocabulary) —
/// it never changes identity, which is the exact written text either way.
/// </summary>
public sealed class NsqlQuotedIdentifierTests
{
    [Fact]
    public void Parse_QuotedNames_CarryTheUnquotedText()
    {
        // Arrange
        var project = new TestNsqlParser(
            """"
            CREATE SCHEMA "My Schema";
            CREATE TABLE "My Schema"."Order Details" ("weird ""col""" int NOT NULL);
            """").Parse();

        // Assert
        var schema = project.Database.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("My Schema");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("Order Details");
        table.Columns.ShouldHaveSingleItem().Name.ShouldBe("weird \"col\"");
    }

    [Fact]
    public void Parse_QuotedAndBareSpellings_AreTheSameName()
    {
        // Arrange — quotes are syntax, not identity: "users" and users are the same name.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app."users" (id int NOT NULL);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Name.ShouldBe("users");
    }

    [Fact]
    public void Parse_QuotedKeyword_IsAColumnNotAKeyword()
    {
        // Arrange — quoting escapes the vocabulary: "constraint" and "include" are plain columns.
        var project = new TestNsqlParser(
            """
            CREATE SCHEMA app;
            CREATE TABLE app.t ("constraint" int NOT NULL, "include" int NOT NULL);
            """).Parse();

        // Assert
        project.Database.Schemas.Single().Tables.Single().Columns
            .Select(c => c.Name.Value).ShouldBe(["constraint", "include"]);
    }

    [Fact]
    public void Parse_Unterminated_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA \"app;").Parse())
            .Message.ShouldContain("Unterminated quoted identifier");

    [Fact]
    public void Parse_Empty_Throws()
        => Should.Throw<NsqlSyntaxException>(() => new TestNsqlParser("CREATE SCHEMA \"\";").Parse())
            .Message.ShouldContain("cannot be empty");

    [Fact]
    public void Write_QuotesOnlyWhatNeedsIt()
    {
        // Arrange — a name with a space, a name colliding with a member-opening keyword, and a plain one.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "app",
                    Tables =
                    [
                        new Table
                        {
                            Name = "Order Details",
                            Columns =
                            [
                                new Column { Name = "id", Type = SqlType.Int },
                                new Column { Name = "constraint", Type = SqlType.Int },
                            ],
                        },
                    ],
                },
            ],
        };

        // Act
        var written = NsqlFormatter.Format(database);

        // Assert
        written.ShouldContain("CREATE TABLE app.\"Order Details\"");
        written.ShouldContain("\"constraint\" int NOT NULL");
        written.ShouldContain("id int NOT NULL");
    }

    [Fact]
    public void Write_HostileNames_RoundTripThroughTheParser()
    {
        // Arrange — quoting, escaping, keyword collisions: what the writer emits, the parser reads back.
        var database = new Database
        {
            Schemas =
            [
                new Schema
                {
                    Name = "My Schema",
                    Tables =
                    [
                        new Table
                        {
                            Name = "weird \"table\"",
                            Columns = [new Column { Name = "include", Type = SqlType.Int }],
                        },
                    ],
                },
            ],
        };

        // Act
        var reparsed = new TestNsqlParser(NsqlFormatter.Format(database)).Parse().Database;

        // Assert
        var schema = reparsed.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("My Schema");
        var table = schema.Tables.ShouldHaveSingleItem();
        table.Name.ShouldBe("weird \"table\"");
        table.Columns.ShouldHaveSingleItem().Name.ShouldBe("include");
    }

    [Fact]
    public void Format_PreservesQuotedIdentifiers()
    {
        // Arrange
        const string source = "CREATE TABLE app.\"Order Details\" (\"weird \"\"col\"\"\" int NOT NULL);";

        // Act
        var formatted = NsqlFormatter.Format(source).Value!;

        // Assert — the formatter emits source verbatim between structural breaks, quotes intact.
        formatted.ShouldContain("\"Order Details\"");
        formatted.ShouldContain("\"weird \"\"col\"\"\"");
    }
}
