using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration.Sources;

namespace NSchema.Tests.Json;

public sealed class AddJsonSchemasFromGlobTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AddJsonSchemasFromGlobTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private void WriteSchemaFile(string relativePath, string schemaName)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{ "schemas": [{ "name": "{{schemaName}}", "tables": [] }], "droppedSchemas": [] }""");
    }

    // Registers the providers and returns the names of every schema they collectively yield.
    private async Task<List<string>> ResolveSchemaNames(Action<NSchemaApplicationBuilder> configure)
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
    public async Task DoubleStar_MatchesFilesAtEveryDepth()
    {
        WriteSchemaFile("a.json", "a");
        WriteSchemaFile("nested/b.json", "b");
        WriteSchemaFile("nested/deep/c.json", "c");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromGlob($"{_root}/**/*.json"));

        names.ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    [Fact]
    public async Task SingleStar_MatchesOnlyTheTargetedDirectory()
    {
        WriteSchemaFile("top.json", "top");
        WriteSchemaFile("nested/skipme.json", "skipme");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromGlob($"{_root}/*.json"));

        names.ShouldBe(["top"]);
    }

    [Fact]
    public async Task MatchesAreOrderedByOrdinalPath()
    {
        WriteSchemaFile("b.json", "b");
        WriteSchemaFile("a.json", "a");
        WriteSchemaFile("c.json", "c");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromGlob($"{_root}/**/*.json"));

        names.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public async Task NoMatches_RegistersNothing()
    {
        WriteSchemaFile("a.yaml", "a");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromGlob($"{_root}/**/*.json"));

        names.ShouldBeEmpty();
    }
}
