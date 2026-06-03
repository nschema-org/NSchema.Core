using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Migration;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Plan</c> through a real <see cref="NSchemaApplication"/>: desired schema from a JSON file on disk,
/// current schema from an in-memory provider, asserting on the structured diff the pipeline reports.
/// </summary>
public sealed class PlanEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly RecordingReporter _reporter = new();

    public PlanEndToEndTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteJson(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplicationBuilder NewBuilder(DatabaseSchema current)
    {
        var builder = NSchemaApplication.CreateBuilder();
        builder.Services.AddSingleton<IMigrationReporter>(_reporter);
        builder.Services.AddKeyedSingleton<ISchemaProvider>(NSchemaKeys.OnlineSchemaProvider, new InMemorySchemaProvider(current));
        return builder;
    }

    [Fact]
    public async Task Plan_ReportsStructuredDiffBetweenCurrentAndDesired()
    {
        // Current: app.users(id). Desired: app.users(id, email) + a new app.orders table.
        var current = DatabaseSchema.Create([SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])])]);

        var desired = WriteJson("schema.json",
            """
            {
              "schemas": [{
                "name": "app",
                "tables": [
                  { "name": "users",  "columns": [{ "name": "id", "type": "int" }, { "name": "email", "type": "text" }] },
                  { "name": "orders", "columns": [{ "name": "id", "type": "int" }] }
                ]
              }],
              "droppedSchemas": []
            }
            """);

        using var app = NewBuilder(current).AddJsonSchema(desired).Build();

        await app.Plan(TestContext.Current.CancellationToken);

        var schema = _reporter.Diff.ShouldNotBeNull().Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");

        var users = schema.Tables.Single(t => t.Name == "users");
        users.Kind.ShouldBe(ChangeKind.Modify);
        users.Columns.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            c => c.Name.ShouldBe("email"),
            c => c.Kind.ShouldBe(ChangeKind.Add));

        schema.Tables.Single(t => t.Name == "orders").Kind.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public async Task Plan_WithNoChanges_ReportsEmptyDiff()
    {
        var schema = DatabaseSchema.Create([SchemaDefinition.Create("app", tables:
            [Table.Create("users", columns: [Column.Create("id", SqlType.Int)])])]);

        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [{ "name": "users", "columns": [{ "name": "id", "type": "int" }] }] }], "droppedSchemas": [] }
            """);

        using var app = NewBuilder(schema).AddJsonSchema(desired).Build();

        await app.Plan(TestContext.Current.CancellationToken);

        _reporter.Diff.ShouldNotBeNull().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_WithGeneratorRegistered_ReportsSqlPreview()
    {
        var current = DatabaseSchema.Create([]);
        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [] }], "droppedSchemas": [] }
            """);

        using var app = NewBuilder(current)
            .AddJsonSchema(desired)
            .UseSqlGenerator<StubSqlGenerator>()
            .Build();

        await app.Plan(TestContext.Current.CancellationToken);

        // The stub emits one statement per action; creating a schema yields a CreateSchema action.
        _reporter.SqlPlan.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Plan_WithoutGenerator_ReportsNoSqlPreview()
    {
        var current = DatabaseSchema.Create([]);
        var desired = WriteJson("schema.json",
            """
            { "schemas": [{ "name": "app", "tables": [] }], "droppedSchemas": [] }
            """);

        using var app = NewBuilder(current).AddJsonSchema(desired).Build();

        await app.Plan(TestContext.Current.CancellationToken);

        _reporter.SqlPlan.ShouldBeNull();
        _reporter.Infos.ShouldContain(i => i.Contains("Unable to generate SQL preview"));
    }
}
