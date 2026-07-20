using NSchema.Config;

namespace NSchema.Tests.Config;

/// <summary>
/// The one-call configuration read: files in, <see cref="ConfigDefinition"/> out, with read and assembly
/// diagnostics merged onto one result.
/// </summary>
public sealed class ConfigProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("nschema-config-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task GetConfig_ReadsAndAssembles()
    {
        // Arrange
        var plugins = Write("plugins.sql",
            """
            PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );
            ENGINE ( version = '[5.0,6.0)' );
            """);
        var config = Write("config.sql",
            """
            DATABASE pg ( host = 'localhost' );
            STATE file ( path = 'state/app.nsstate' );
            """);

        // Act
        var result = await ConfigProvider.GetConfig([plugins, config], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.Plugins.ShouldHaveSingleItem().ShouldBe(new PluginDeclaration("pg", "NSchema.Postgres", VersionRange.Parse("5.0.1")));
        result.Value.Engine.ShouldBe(new EngineConfig(new EngineRequirement(VersionRange.Parse("[5.0,6.0)"))));
        result.Value.Database!.Attribute("host")!.AsString().ShouldBe("localhost");
        result.Value.State!.Label.ShouldBe("file");
    }

    [Fact]
    public async Task GetConfig_UnreadableFile_IsAnError_AndTheRestStillAssembles()
    {
        // Arrange
        var config = Write("config.sql", "PLUGIN pg ( source = 'NSchema.Postgres', version = '5.0.1' );");
        var missing = Path.Combine(_root, "missing.sql");

        // Act
        var result = await ConfigProvider.GetConfig([config, missing], cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("missing.sql");
        result.Value!.Plugins.ShouldHaveSingleItem().Label.ShouldBe("pg");
    }

    [Fact]
    public async Task GetConfig_MergesReadAndAssemblyDiagnostics()
    {
        // Arrange — one file with a syntax error, another whose DATABASE names no plugin.
        var broken = Write("broken.sql", "WORKSPACE staging ( region = 'eu' );");
        var config = Write("config.sql", "DATABASE pg ( host = 'localhost' );");

        // Act
        var result = await ConfigProvider.GetConfig([broken, config], cancellationToken: TestContext.Current.CancellationToken);

        // Assert — both stages report, each finding stamped with its file.
        result.Errors.Count().ShouldBe(2);
        result.Errors.ShouldContain(d => d.File!.EndsWith("broken.sql") && d.Message.Contains("Unknown configuration statement"));
        result.Errors.ShouldContain(d => d.File!.EndsWith("config.sql") && d.Message.Contains("no PLUGIN statement declares it"));
    }
}
