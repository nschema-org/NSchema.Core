using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Schema;

public sealed class FileSchemaProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public FileSchemaProviderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    /// Minimal concrete <see cref="FileSchemaProvider"/> that records what it was handed and returns
    /// a caller-supplied schema, so the base-class plumbing can be tested in isolation from parsing.
    /// </summary>
    private sealed class TestProvider(string path, DatabaseSchema result) : FileSchemaProvider(path)
    {
        public string? ParsedContent { get; private set; }
        public bool ParseCalled { get; private set; }

        protected override async ValueTask<DatabaseSchema> Parse(Stream stream, CancellationToken cancellationToken)
        {
            ParseCalled = true;
            using var reader = new StreamReader(stream);
            ParsedContent = await reader.ReadToEndAsync(cancellationToken);
            return result;
        }
    }

    private static DatabaseSchema Schemas(params string[] names)
        => DatabaseSchema.Create([.. names.Select(n => SchemaDefinition.Create(n))]);

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsMissingPath(string? path)
        => Should.Throw<ArgumentException>(() => new TestProvider(path!, Schemas()));

    // -------------------------------------------------------------------------
    // GetSchema
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_OpensFileAndPassesStreamToParse()
    {
        var path = WriteFile("schema.txt", "raw-file-body");
        var sut = new TestProvider(path, Schemas("app"));

        await sut.GetSchema(null, TestContext.Current.CancellationToken);

        sut.ParseCalled.ShouldBeTrue();
        sut.ParsedContent.ShouldBe("raw-file-body");
    }

    [Fact]
    public async Task GetSchema_ReturnsParsedSchema_WhenNoFilter()
    {
        var path = WriteFile("schema.txt", "body");
        var sut = new TestProvider(path, Schemas("app", "audit"));

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "audit"]);
    }

    [Fact]
    public async Task GetSchema_FiltersParsedSchema_BySchemaNames()
    {
        var path = WriteFile("schema.txt", "body");
        var sut = new TestProvider(path, Schemas("app", "audit", "legacy"));

        var result = await sut.GetSchema(["app", "legacy"], TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task GetSchema_WhenFileMissing_ThrowsFileNotFoundWithPath()
    {
        var missing = Path.Combine(_tempDir, "nope.txt");
        var sut = new TestProvider(missing, Schemas("app"));

        var ex = await Should.ThrowAsync<FileNotFoundException>(() => sut.GetSchema());

        ex.Message.ShouldContain(missing);
        sut.ParseCalled.ShouldBeFalse();
    }
}
