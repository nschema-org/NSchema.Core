using System.Text;
using Microsoft.Extensions.Options;
using NSchema.State.File;

namespace NSchema.Tests.State;

public sealed class FileSchemaStateStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"nschema-state-{Guid.NewGuid():N}");
    private readonly string _path;
    private readonly IOptions<FileSchemaStateStoreOptions> _options;

    public FileSchemaStateStoreTests()
    {
        _path = Path.Combine(_directory, "nested", "state.json");
        _options = Options.Create(new FileSchemaStateStoreOptions { Path = _path });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public async Task Read_MissingFile_ReturnsNull()
    {
        var sut = new FileSchemaStateStore(_options);

        var result = await sut.Read(TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Write_CreatesFileAndMissingDirectories()
    {
        var sut = new FileSchemaStateStore(_options);

        await sut.Write(new byte[] { 0x7b, 0x7d }, TestContext.Current.CancellationToken);

        File.Exists(_path).ShouldBeTrue();
    }

    [Fact]
    public async Task Write_ThenRead_RoundTripsThePayload()
    {
        var sut = new FileSchemaStateStore(_options);
        var payload = Encoding.UTF8.GetBytes("""{"version":1,"schema":"state"}""");

        await sut.Write(payload, TestContext.Current.CancellationToken);
        var result = await sut.Read(TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Value.ToArray().ShouldBe(payload);
    }
}
