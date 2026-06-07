using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

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
    /// Minimal <see cref="ISchemaSerializer"/> that records what it was handed and returns
    /// a caller-supplied schema, so the <see cref="FileSchemaProvider"/> plumbing can be tested in
    /// isolation from parsing.
    /// </summary>
    private sealed class RecordingSerializer(DatabaseSchema result) : ISchemaSerializer
    {
        public string? ParsedContent { get; private set; }
        public bool ReadCalled { get; private set; }

        public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
        {
            ReadCalled = true;
            using var reader = new StreamReader(source);
            ParsedContent = await reader.ReadToEndAsync(cancellationToken);
            return result;
        }
    }

    private static (FileSchemaProvider Provider, RecordingSerializer Serializer) Sut(string path, DatabaseSchema result)
    {
        var serializer = new RecordingSerializer(result);
        return (new FileSchemaProvider(path, serializer), serializer);
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
        => Should.Throw<ArgumentException>(() => new FileSchemaProvider(path!, new RecordingSerializer(Schemas())));

    // -------------------------------------------------------------------------
    // GetSchema
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetSchema_OpensFileAndPassesStreamToSerializer()
    {
        var path = WriteFile("schema.txt", "raw-file-body");
        var (sut, serializer) = Sut(path, Schemas("app"));

        await sut.GetSchema(null, TestContext.Current.CancellationToken);

        serializer.ReadCalled.ShouldBeTrue();
        serializer.ParsedContent.ShouldBe("raw-file-body");
    }

    [Fact]
    public async Task GetSchema_ReturnsParsedSchema_WhenNoFilter()
    {
        var path = WriteFile("schema.txt", "body");
        var (sut, _) = Sut(path, Schemas("app", "audit"));

        var result = await sut.GetSchema(null, TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "audit"]);
    }

    [Fact]
    public async Task GetSchema_FiltersParsedSchema_BySchemaNames()
    {
        var path = WriteFile("schema.txt", "body");
        var (sut, _) = Sut(path, Schemas("app", "audit", "legacy"));

        var result = await sut.GetSchema(["app", "legacy"], TestContext.Current.CancellationToken);

        result.Schemas.Select(s => s.Name).ShouldBe(["app", "legacy"]);
    }

    [Fact]
    public async Task GetSchema_WhenFileMissing_ThrowsFileNotFoundWithPath()
    {
        var missing = Path.Combine(_tempDir, "nope.txt");
        var (sut, serializer) = Sut(missing, Schemas("app"));

        var ex = await Should.ThrowAsync<FileNotFoundException>(() => sut.GetSchema().AsTask());

        ex.Message.ShouldContain(missing);
        serializer.ReadCalled.ShouldBeFalse();
    }
}
