using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Scripts;
using NSchema.Scripts.Model;

namespace NSchema.Tests;

public sealed class AddSqlScriptsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AddSqlScriptsTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteScriptFile(string relativePath, string sql)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sql);
        return path;
    }

    // Registers the providers and returns every script they collectively yield, in provider/read order.
    private static async Task<List<Script>> ResolveScripts(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();

        var scripts = new List<Script>();
        foreach (var provider in app.Services.GetServices<IScriptProvider>())
        {
            scripts.AddRange(await provider.GetScripts());
        }
        return scripts;
    }

    [Fact]
    public async Task AddSqlScripts_LoadsSingleFile()
    {
        // A wildcard-free pattern names a single file under the base directory.
        WriteScriptFile("seed.sql", "SELECT 1;");

        var scripts = await ResolveScripts(b => b.AddSqlScripts(ScriptType.PreDeployment, _root, "seed.sql"));

        var script = scripts.ShouldHaveSingleItem();
        script.Name.ShouldBe("seed.sql");
        script.Sql.ShouldBe("SELECT 1;");
        script.Type.ShouldBe(ScriptType.PreDeployment);
    }

    [Fact]
    public async Task AddSqlScripts_MatchesFilesAtEveryDepth()
    {
        WriteScriptFile("a.sql", "-- a");
        WriteScriptFile("nested/b.sql", "-- b");
        WriteScriptFile("nested/deep/c.sql", "-- c");

        var scripts = await ResolveScripts(b => b.AddSqlScripts(ScriptType.PostDeployment, _root, "**/*.sql"));

        scripts.Select(s => s.Name).ShouldBe(["a.sql", "b.sql", "c.sql"], ignoreOrder: true);
    }

    [Fact]
    public async Task AddSqlScripts_OrdersMatchesByPath()
    {
        // Scripts run in order, so the matched set is sorted deterministically (Ordinal) regardless of write order.
        WriteScriptFile("003.sql", "-- 3");
        WriteScriptFile("001.sql", "-- 1");
        WriteScriptFile("002.sql", "-- 2");

        var scripts = await ResolveScripts(b => b.AddSqlScripts(ScriptType.PreDeployment, _root, "**/*.sql"));

        scripts.Select(s => s.Name).ShouldBe(["001.sql", "002.sql", "003.sql"]);
    }

    [Fact]
    public async Task AddSqlScripts_WithMatcher_HonoursExcludes()
    {
        WriteScriptFile("001.pre.sql", "-- pre");
        WriteScriptFile("002.pre.sql", "-- pre");
        WriteScriptFile("ignore.post.sql", "-- post");

        var matcher = new Matcher();
        matcher.AddInclude("**/*.pre.sql");
        matcher.AddExclude("**/002.pre.sql");

        var scripts = await ResolveScripts(b => b.AddSqlScripts(ScriptType.PreDeployment, _root, matcher));

        scripts.Select(s => s.Name).ShouldBe(["001.pre.sql"]);
    }

    [Fact]
    public async Task AddSqlScripts_DerivesName()
    {
        // Core stays oblivious to the .pre/.post convention: only the .sql extension is dropped, so a
        // double-suffixed file keeps its qualifier in the name.
        WriteScriptFile("0001_seed.pre.sql", "SELECT 1;");

        var script = (await ResolveScripts(b => b.AddSqlScripts(ScriptType.PreDeployment, _root, "**/*.sql"))).ShouldHaveSingleItem();

        script.Name.ShouldBe("0001_seed.pre.sql");
    }

    [Fact]
    public async Task AddSqlScripts_MatchingNothing_ReturnsEmpty()
    {
        // Unlike the desired schema (where an empty match throws), having no scripts is valid and common.
        var scripts = await ResolveScripts(b => b.AddSqlScripts(ScriptType.PostDeployment, _root, "**/*.sql"));

        scripts.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddSqlScripts_RegistersOneProvider_AndMatchesAtReadTime()
    {
        // The glob is evaluated when scripts are read, not at registration: a single provider is registered,
        // and a file written after the builder is configured is still picked up.
        var builder = NSchemaApplication.CreateBuilder();
        builder.AddSqlScripts(ScriptType.PreDeployment, _root, "**/*.sql");
        using var app = builder.Build();

        WriteScriptFile("late.sql", "-- late");

        var provider = app.Services.GetServices<IScriptProvider>().ShouldHaveSingleItem();
        var scripts = await provider.GetScripts(TestContext.Current.CancellationToken);
        scripts.Select(s => s.Name).ShouldBe(["late.sql"]);
    }
}
