using NSchema.Project.Ddl;

namespace NSchema.Tests.Schema.Serialization.Ddl;

/// <summary>
/// The read seam's contract: parsing outcomes are a <see cref="Result{T}"/>, never a thrown exception.
/// The grammar itself is covered by the <c>DdlParser*Tests</c> against the parser directly.
/// </summary>
public sealed class DdlReaderTests
{
    [Fact]
    public void Read_ValidDocument_Succeeds()
    {
        // Act
        var result = DdlReader.Instance.Read("CREATE SCHEMA app;");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Read_SyntaxError_FailsWithAPositionedDiagnostic()
    {
        // Act
        var result = DdlReader.Instance.Read("CREATE SCHEMA app");

        // Assert — the position rides in the message; there is no structured position on the diagnostic (yet).
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Source.ShouldBe("syntax");
        error.Message.ShouldContain("line 1");
    }

    [Fact]
    public void Read_MultipleSyntaxErrors_ReportsThemAll()
    {
        // Arrange — three statements; the first and last are broken, the middle is fine.
        const string source =
            "CREATE TABLE app.users (id, name text);\n" +
            "CREATE SCHEMA app;\n" +
            "GRANT TRUNCATE ON app.users TO readers;";

        // Act
        var result = DdlReader.Instance.Read(source);

        // Assert — the parser resyncs at statement boundaries, so both errors surface in one read.
        result.IsFailure.ShouldBeTrue();
        var errors = result.Errors.ToList();
        errors.Count.ShouldBe(2);
        errors[0].Message.ShouldContain("line 1");
        errors[1].Message.ShouldContain("line 3");
    }

    [Fact]
    public void Read_ErrorInsideTemplate_RecoversToTheNextTemplateStatement()
    {
        // Arrange — a broken statement inside the body; the template's remaining statement still parses,
        // and the statement after the template is reached.
        const string source =
            "TEMPLATE audit BEGIN\n" +
            "    CREATE TABLE log (id, at timestamptz);\n" +
            "    CREATE TABLE trail (id int);\n" +
            "END;\n" +
            "CREATE SCHEMA app";

        // Act
        var result = DdlReader.Instance.Read(source);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var errors = result.Errors.ToList();
        errors.Count.ShouldBe(2);
        errors[0].Message.ShouldContain("line 2");
        errors[1].Message.ShouldContain("line 5");
    }
}
