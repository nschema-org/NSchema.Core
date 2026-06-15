using Microsoft.Extensions.DependencyInjection;
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
    public async Task AddSqlSchema_LoadsSingleFile()
    {
        var path = WriteSchemaFile("app.sql", "app");

        var names = await ResolveSchemaNames(b => b.AddSqlSchema(path));

        names.ShouldBe(["app"]);
    }

    [Fact]
    public async Task AddSqlSchemasFromGlob_MatchesFilesAtEveryDepth()
    {
        WriteSchemaFile("a.sql", "a");
        WriteSchemaFile("nested/b.sql", "b");
        WriteSchemaFile("nested/deep/c.sql", "c");

        var names = await ResolveSchemaNames(b => b.AddSqlSchemasFromGlob($"{_root}/**/*.sql"));

        names.ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    [Fact]
    public async Task AddSqlSchemasFromDirectory_PicksUpMatchingFiles()
    {
        WriteSchemaFile("a.sql", "a");
        WriteSchemaFile("nested/b.sql", "b");

        var names = await ResolveSchemaNames(b => b.AddSqlSchemasFromDirectory(_root));

        names.ShouldBe(["a", "b"], ignoreOrder: true);
    }
}
