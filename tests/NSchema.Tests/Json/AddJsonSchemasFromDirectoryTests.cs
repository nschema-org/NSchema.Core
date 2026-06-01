using Microsoft.Extensions.DependencyInjection;
using NSchema.Migration;

namespace NSchema.Tests.Json;

public sealed class AddJsonSchemasFromDirectoryTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

    public AddJsonSchemasFromDirectoryTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string WriteSchemaFile(string relativePath, string schemaName)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $$"""{ "schemas": [{ "name": "{{schemaName}}", "tables": [] }], "droppedSchemas": [] }""");
        return path;
    }

    // Registers the providers and returns the names of every schema they collectively yield.
    private async Task<List<string>> ResolveSchemaNames(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();

        var providers = app.Services.GetServices<ISchemaProvider>();
        var names = new List<string>();
        foreach (var provider in providers)
        {
            var schema = await provider.GetSchema();
            names.AddRange(schema.Schemas.Select(s => s.Name));
        }
        return names;
    }

    [Fact]
    public async Task RegistersEveryJsonFile_Recursively_ByDefault()
    {
        WriteSchemaFile("a.json", "a");
        WriteSchemaFile("nested/b.json", "b");
        WriteSchemaFile("nested/deep/c.json", "c");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromDirectory(_root));

        names.ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    [Fact]
    public async Task Recursive_False_OnlyRegistersTopLevelFiles()
    {
        WriteSchemaFile("top.json", "top");
        WriteSchemaFile("nested/skipme.json", "skipme");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromDirectory(_root, recursive: false));

        names.ShouldBe(["top"]);
    }

    [Fact]
    public async Task HonoursSearchPattern()
    {
        WriteSchemaFile("included.schema.json", "included");
        WriteSchemaFile("ignored.json", "ignored");

        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromDirectory(_root, searchPattern: "*.schema.json"));

        names.ShouldBe(["included"]);
    }

    [Fact]
    public async Task AddJsonSchema_CalledPerFile_RegistersEachAsDistinctProvider()
    {
        // Guards against TryAddEnumerable deduplicating by implementation type, which would collapse
        // multiple JsonSchemaProviders into one.
        var first = WriteSchemaFile("first.json", "first");
        var second = WriteSchemaFile("second.json", "second");

        var names = await ResolveSchemaNames(b => b.AddJsonSchema(first).AddJsonSchema(second));

        names.ShouldBe(["first", "second"], ignoreOrder: true);
    }

    [Fact]
    public async Task EmptyDirectory_RegistersNothing()
    {
        var names = await ResolveSchemaNames(b => b.AddJsonSchemasFromDirectory(_root));

        names.ShouldBeEmpty();
    }

    [Fact]
    public void MissingDirectory_Throws()
    {
        var builder = NSchemaApplication.CreateBuilder();

        var act = () => builder.AddJsonSchemasFromDirectory(Path.Combine(_root, "does-not-exist"));

        act.ShouldThrow<DirectoryNotFoundException>();
    }
}
