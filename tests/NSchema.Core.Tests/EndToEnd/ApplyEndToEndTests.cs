using Microsoft.Extensions.DependencyInjection;
using NSchema.Operations.Apply;
using NSchema.Operations.Plan;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Sql;
using NSchema.State;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// Drives <c>Apply</c> through a real <see cref="NSchemaApplication"/> with the database boundary faked: a stub
/// generator/executor stand in for the dialect and connection, and the on-disk state store captures the result.
/// </summary>
public sealed class ApplyEndToEndTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private readonly RecordingSqlExecutor _executor = new();
    private readonly RecordingStateStore _store = new();

    public ApplyEndToEndTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteDdl(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplication BuildApp(DatabaseSchema current, string desiredPath) =>
        NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions())
            .AddDdlSchemas(Path.GetDirectoryName(desiredPath)!, Path.GetFileName(desiredPath))
            .UseStateStore(_store)
            .UseSqlGenerator<StubSqlGenerator>()
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
        var current = new DatabaseSchema([new SchemaDefinition("app")]);
        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = BuildApp(current, desired);

        // The CLI-style flow: hold the lock, compute the live plan, apply it, release.
        (await app.Locks.Acquire("apply", cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Sql = plan.Sql! }, TestContext.Current.CancellationToken);

        // The plan reached the executor as a non-empty SQL plan.
        _executor.Executed.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
        // The plan exposes the same SQL the caller previews before applying.
        plan.Sql.ShouldBe(_executor.Executed);
        // Post-apply state was captured to the store.
        _store.Written.ShouldNotBeNull().Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Apply_WithNoChanges_ShortCircuitsWithoutExecutingButStillCapturesState()
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

        using var app = BuildApp(schema, desired);

        (await app.Locks.Acquire("apply", cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        // The plan carries an empty diff/sql: there is nothing to apply.
        plan.Diff.ShouldNotBeNull().IsEmpty.ShouldBeTrue();
        plan.Sql.ShouldNotBeNull().IsEmpty.ShouldBeTrue();

        await app.Operations.Apply(new ApplyArguments { Sql = plan.Sql! }, TestContext.Current.CancellationToken);

        // Nothing to apply: the empty plan never reaches the executor...
        _executor.Executed.ShouldBeNull();
        // ...but a first run against an already-matching database still initialises the store.
        _store.Written.ShouldNotBeNull().Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }
}
