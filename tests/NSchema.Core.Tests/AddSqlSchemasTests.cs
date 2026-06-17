using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Schema;

namespace NSchema.Tests;

public sealed class AddSqlSchemasTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AddSqlSchemasTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteSchemaFile(string relativePath, string schemaName)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"CREATE SCHEMA {schemaName};");
        return path;
    }

    // Registers the providers and returns the names of every schema they collectively yield.
    private static async Task<List<string>> ResolveSchemaNames(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();

        var names = new List<string>();
        foreach (var provider in app.Services.GetServices<ISchemaProvider>())
        {
            var schema = await provider.GetSchema();
            names.AddRange(schema.Schemas.Select(s => s.Name));
        }
        return names;
    }

    [Fact]
    public async Task AddSqlSchemas_LoadsSingleFile()
    {
        // A wildcard-free pattern names a single file under the base directory.
        WriteSchemaFile("app.sql", "app");

        var names = await ResolveSchemaNames(b => b.AddSqlSchemas(_root, "app.sql"));

        names.ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_MatchesFilesAtEveryDepth()
    {
        WriteSchemaFile("a.sql", "a");
        WriteSchemaFile("nested/b.sql", "b");
        WriteSchemaFile("nested/deep/c.sql", "c");

        var names = await ResolveSchemaNames(b => b.AddSqlSchemas(_root));

        names.ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    [Fact]
    public async Task AddSqlSchemas_WithMatcher_HonoursExcludes()
    {
        // The CLI's case: include every .sql but exclude the pre/post deployment scripts.
        WriteSchemaFile("app.sql", "app");
        WriteSchemaFile("app.pre.sql", "pre");
        WriteSchemaFile("app.post.sql", "post");

        var matcher = new Matcher();
        matcher.AddInclude("**/*.sql");
        matcher.AddExclude("**/*.pre.sql");
        matcher.AddExclude("**/*.post.sql");

        var names = await ResolveSchemaNames(b => b.AddSqlSchemas(_root, matcher));

        names.ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_MatchingNothing_Throws()
    {
        // Planning against an empty desired schema would read as "drop everything", so a pattern that
        // resolves to no files is a configuration error rather than an empty schema.
        var ex = await Should.ThrowAsync<FileNotFoundException>(
            () => ResolveSchemaNames(b => b.AddSqlSchemas(_root)));

        ex.Message.ShouldContain(_root);
    }

    [Fact]
    public async Task AddSqlSchemas_FiltersBySchemaNames()
    {
        File.WriteAllText(Path.Combine(_root, "multi.sql"), "CREATE SCHEMA app; CREATE SCHEMA audit;");

        var builder = NSchemaApplication.CreateBuilder();
        builder.AddSqlSchemas(_root);
        using var app = builder.Build();

        var provider = app.Services.GetServices<ISchemaProvider>().ShouldHaveSingleItem();
        var schema = await provider.GetSchema(["app"], TestContext.Current.CancellationToken);

        schema.Schemas.Select(s => s.Name).ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemas_RegistersOneProvider_AndMatchesAtReadTime()
    {
        // The glob is evaluated when the schema is read, not at registration: a single provider is
        // registered, and a file written after the builder is configured is still picked up.
        var builder = NSchemaApplication.CreateBuilder();
        builder.AddSqlSchemas(_root);
        using var app = builder.Build();

        WriteSchemaFile("late.sql", "late");

        var provider = app.Services.GetServices<ISchemaProvider>().ShouldHaveSingleItem();
        var schema = await provider.GetSchema(cancellationToken: TestContext.Current.CancellationToken);
        schema.Schemas.Select(s => s.Name).ShouldBe(["late"]);
    }
}
