using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Schemas;

namespace NSchema.Tests.Schema.Serialization.Nsql;

/// <summary>
/// The file-aware read seam's contract: results are typed <see cref="NsqlDiagnostic"/>s carrying position
/// (and file, when read from one) structurally, and a failed parse still carries the statements that parsed.
/// </summary>
public sealed class NsqlReaderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-nsql-reader-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Read_ValidSource_ProducesTheTree()
    {
        // Act
        var result = NsqlReader.Instance.Read("CREATE SCHEMA app;");

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var statement = result.Value.Statements.ShouldHaveSingleItem().ShouldBeOfType<CreateSchemaStatement>();
        statement.Name.Text.ShouldBe("app");
        result.Value.FilePath.ShouldBeNull();
    }

    [Fact]
    public void Read_SyntaxError_CarriesThePositionStructurally_AndThePartialTree()
    {
        // Act — the first statement parses; the second is broken.
        var result = NsqlReader.Instance.Read("CREATE SCHEMA app;\nCREATE SCHEMA 123;");

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.Position.Line.ShouldBe(2);
        error.File.ShouldBeNull();
        result.Value!.Statements.ShouldHaveSingleItem().ShouldBeOfType<CreateSchemaStatement>().Name.Text.ShouldBe("app");
    }

    [Fact]
    public async Task ReadFile_StampsThePath_OnTheDocumentAndEveryDiagnostic()
    {
        // Arrange
        var path = Path.Combine(_root, "schema.sql");
        await File.WriteAllTextAsync(path, "CREATE TABLE app.users (", TestContext.Current.CancellationToken);

        // Act
        var result = await NsqlReader.Instance.ReadFile(path, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().File.ShouldBe(path);
        result.Value!.FilePath.ShouldBe(path);
    }

    [Fact]
    public async Task ReadFile_MissingFile_FailsWithADiagnostic()
    {
        // Act
        var result = await NsqlReader.Instance.ReadFile(Path.Combine(_root, "missing.sql"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var error = result.Errors.ShouldHaveSingleItem();
        error.File.ShouldBe(Path.Combine(_root, "missing.sql"));
        error.Message.ShouldContain("missing.sql");
    }
}
