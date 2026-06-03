using NSchema.Schema.Model;
using NSchema.Scripts;

namespace NSchema.Tests.Scripts;

public sealed class FileScriptProviderTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public FileScriptProviderTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task GetScripts_ReadsFileContentAsSql()
    {
        var path = WriteFile("seed.sql", "SELECT 1;");
        var sut = new FileScriptProvider(ScriptType.PreDeployment, path);

        var scripts = await sut.GetScripts(TestContext.Current.CancellationToken);

        var script = scripts.ShouldHaveSingleItem();
        script.Sql.ShouldBe("SELECT 1;");
        script.Type.ShouldBe(ScriptType.PreDeployment);
    }

    [Fact]
    public async Task GetScripts_DerivesNameFromFileNameWithoutExtension_WhenNameNotProvided()
    {
        var path = WriteFile("0001_seed.sql", "SELECT 1;");
        var sut = new FileScriptProvider(ScriptType.PostDeployment, path);

        var script = (await sut.GetScripts(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        script.Name.ShouldBe("0001_seed");
    }

    [Fact]
    public async Task GetScripts_UsesExplicitName_WhenProvided()
    {
        var path = WriteFile("0001_seed.sql", "SELECT 1;");
        var sut = new FileScriptProvider(ScriptType.PreDeployment, path, name: "custom-name");

        var script = (await sut.GetScripts(TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        script.Name.ShouldBe("custom-name");
    }

    [Fact]
    public async Task GetScripts_WhenFileMissing_ThrowsFileNotFoundException()
    {
        var sut = new FileScriptProvider(ScriptType.PreDeployment, Path.Combine(_tempDir, "missing.sql"));

        var act = () => sut.GetScripts();

        await act.ShouldThrowAsync<FileNotFoundException>();
    }
}
