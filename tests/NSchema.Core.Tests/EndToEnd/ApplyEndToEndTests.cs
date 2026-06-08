using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Sql;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Apply</c> through a real <see cref="NSchemaApplication"/> with the database boundary faked: a stub
/// generator/executor stand in for the dialect and connection, and the on-disk state store captures the result.
/// </summary>
public sealed class ApplyEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly RecordingReporter _reporter = new();
    private readonly RecordingSqlExecutor _executor = new();
    private readonly RecordingStateStore _store = new();

    public ApplyEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteJson(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplication BuildApp(DatabaseSchema current, string desiredJsonPath) =>
        NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions { Reporter = RecordingReporter.FormatName })
            .AddJsonSchema(desiredJsonPath)
            .UseStateStore(_store)
            .AddSqlGenerator<StubSqlGenerator>(StubSqlGenerator.DialectName)
            .WithDialect(StubSqlGenerator.DialectName)
            .AddReporter(RecordingReporter.FormatName, _reporter)
            .Tap(b =>
            {
                b.Services.AddSingleton<ISqlExecutor>(_executor);
                b.Services.AddKeyedSingleton<ISchemaProvider>(NSchemaKeys.OnlineSchemaProvider, new InMemorySchemaProvider(current));
            })
            .Build();

    [Fact]
    public async Task Apply_GeneratesSql_Executes_AndRefreshesState()
    {
        // Current live DB: an empty app schema. Desired: app.users(id) — i.e. create the table.
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app")]);
        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [{ "name": "users", "columns": [{ "name": "id", "type": "int" }] }] }], "droppedSchemas": [] }
            """);

        using var app = BuildApp(current, desired);

        await app.Apply(TestContext.Current.CancellationToken);

        // The plan reached the executor as a non-empty SQL plan.
        _executor.Executed.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
        // The same plan was reported for preview before execution.
        _reporter.SqlPlan.ShouldBe(_executor.Executed);
        // Post-apply state was captured to the store.
        _store.Written.ShouldNotBeNull().Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Apply_WithNoChanges_ExecutesEmptyPlan()
    {
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])])]);
        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [{ "name": "users", "columns": [{ "name": "id", "type": "int" }] }] }], "droppedSchemas": [] }
            """);

        using var app = BuildApp(schema, desired);

        await app.Apply(TestContext.Current.CancellationToken);

        _reporter.Diff.ShouldNotBeNull().IsEmpty.ShouldBeTrue();
        _executor.Executed.ShouldNotBeNull().Statements.ShouldBeEmpty();
    }
}
