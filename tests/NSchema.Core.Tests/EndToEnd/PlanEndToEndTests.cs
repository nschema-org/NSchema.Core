using Microsoft.Extensions.DependencyInjection;
using NSchema.Current.Backends;
using NSchema.Diff.Domain.Models;
using NSchema.Operations;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Plan</c> through a real <see cref="NSchemaApplication"/>: desired schema from a DDL file on disk,
/// current schema from an in-memory provider, asserting on the structured diff the pipeline reports.
/// </summary>
public sealed class PlanEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

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
        builder.Services.AddSingleton<ISchemaIntrospector>(new InMemoryIntrospector(current));
        // Planning requires a state store; these tests diff against the live provider, so an empty in-memory one suffices.
        builder.UseEphemeralState();
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

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);

        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
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

        using var app = NewBuilder(schema).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);

        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_WithDialectRegistered_ProducesSql()
    {
        var current = new DatabaseSchema([]);
        var desired = WriteDdl("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current)
            .AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired))
            .UseSqlDialect<StubSqlDialect>()
            .Build();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);

        // The stub emits one statement per action; creating a schema yields a CreateSchema action.
        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Plan_WithoutProvider_Fails()
    {
        var current = new DatabaseSchema([]);
        var desired = WriteDdl("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        var result = await app.Operations.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        // A dialect is required for planning — there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Planning requires a database provider"));
    }

    [Fact]
    public async Task Plan_Teardown_DiffsTheManagedSchemaDownToNothing()
    {
        // The managed schema is the recorded state, so the refresh records the live schema before tearing down.
        var current = new DatabaseSchema([new SchemaDefinition("app", Tables:
            [new Table("users", Columns: [new Column("id", SqlType.Int)])])]);
        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Teardown }, TestContext.Current.CancellationToken);

        // The teardown plan drops the managed table.
        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
        schema.Tables.ShouldContain(t => t.Name == "users" && t.Kind == ChangeKind.Remove);
    }
}
