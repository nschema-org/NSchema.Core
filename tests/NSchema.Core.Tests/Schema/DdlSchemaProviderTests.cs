using NSchema.Schema.Ddl;

namespace NSchema.Tests.Schema;

public sealed class DdlSchemaProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public DdlSchemaProviderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteSchemas(params string[] names)
    {
        var ddl = string.Join("\n", names.Select(n =>
            $"CREATE SCHEMA {n};\nCREATE TABLE {n}.t ( id int NOT NULL );"));
        var path = Path.Combine(_tempDir, "schema.sql");
        File.WriteAllText(path, ddl);
        return path;
    }

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingPath(string? path)
        => Should.Throw<ArgumentException>(() => new DdlSchemaProvider(path!));

    // -------------------------------------------------------------------------
    // GetSchema
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_ParsesFile_WhenNoFilter()
    {
        var path = WriteSchemas("app", "audit");
        var sut = new DdlSchemaProvider(path);

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "audit"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetSchema_FiltersParsedSchema_BySchemaNames()
    {
        var path = WriteSchemas("app", "audit", "legacy");
        var sut = new DdlSchemaProvider(path);

        var result = await sut.GetSchema(["app", "legacy"], TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "legacy"], ignoreOrder: true);
    }

    [Fact]
    public async Task GetSchema_WhenFileMissing_ThrowsFileNotFoundWithPath()
    {
        var missing = Path.Combine(_tempDir, "nope.sql");
        var sut = new DdlSchemaProvider(missing);

        var ex = await Should.ThrowAsync<FileNotFoundException>(() => sut.GetSchema().AsTask());

        ex.Message.ShouldContain(missing);
    }
}
