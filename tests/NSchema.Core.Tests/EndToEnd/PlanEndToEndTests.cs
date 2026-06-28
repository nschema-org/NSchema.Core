using Microsoft.Extensions.DependencyInjection;
using NSchema.Diff.Model;
using NSchema.Operations.Plan;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Plan</c> through a real <see cref="NSchemaApplication"/>: desired schema from a DDL file on disk,
/// current schema from an in-memory provider, asserting on the structured diff the pipeline reports.
/// </summary>
public sealed class PlanEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly RecordingReporter _reporter = new();

    public PlanEndToEndTests() => Directory.CreateDirectory(_tempDir);
    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteDdl(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplicationBuilder NewBuilder(DatabaseSchema current)
    {
        var builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions());
        builder.UseReporter(_reporter);
        builder.Services.AddKeyedSingleton<ISchemaProvider>(NSchemaKeys.OnlineSchemaProvider, new InMemorySchemaProvider(current));
        return builder;
    }

    [Fact]
    public async Task Plan_ReportsStructuredDiffBetweenCurrentAndDesired()
    {
        // Current: app.users(id). Desired: app.users(id, email) + a new app.orders table.
        var current = new DatabaseSchema([new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("id", SqlType.Int)])])]);

        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL,
                email text NOT NULL
            );
            CREATE TABLE app.orders
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(current).AddDdlSchemas(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

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
        var schema = new DatabaseSchema([new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("id", SqlType.Int)])])]);

        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(schema).AddDdlSchemas(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        _reporter.Diff.ShouldNotBeNull().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_WithGeneratorRegistered_ReportsSqlPreview()
    {
        var current = new DatabaseSchema([]);
        var desired = WriteDdl("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current)
            .AddDdlSchemas(Path.GetDirectoryName(desired)!, Path.GetFileName(desired))
            .UseSqlGenerator<StubSqlGenerator>()
            .Build();

        await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        // The stub emits one statement per action; creating a schema yields a CreateSchema action.
        _reporter.SqlPlan.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Plan_WithoutGenerator_ReportsNoSqlPreview()
    {
        var current = new DatabaseSchema([]);
        var desired = WriteDdl("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddDdlSchemas(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        var result = await app.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        // No provider: the "no SQL preview" notice is now carried back as a warning diagnostic, not reporter output.
        _reporter.SqlPlan.ShouldBeNull();
        result.Diagnostics.ShouldContain(d => d.Message.Contains("Unable to generate SQL preview"));
    }
}
