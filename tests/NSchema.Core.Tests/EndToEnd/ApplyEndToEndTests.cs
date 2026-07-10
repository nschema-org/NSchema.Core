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
                b.Services.AddSingleton<ISchemaProvider>(new InMemorySchemaProvider(current));
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
        _store.Written.ShouldNotBeNull().Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public async Task Apply_RequiredColumnAddWithMatchedMigration_ExecutesNullableAddBackfillThenTighten()
    {
        // Current live DB: a populated-shaped app.users(id). Desired: the same table gaining a NOT NULL,
        // defaultless email column, with a MIGRATION block declaring the backfill.
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
            MIGRATION 'backfill emails' FOR ADD COLUMN app.users.email AS $$
            UPDATE app.users SET email = 'unknown@example.com';
            $$;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire("apply", cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Sql = plan.Sql! }, TestContext.Current.CancellationToken);

        // The add was decomposed around the backfill: nullable add, the block's SQL, then the tighten — in order.
        var statements = _executor.Executed.ShouldNotBeNull().Statements.Select(s => s.Sql).ToList();
        statements.ShouldBe([
            "-- AddColumn",
            "UPDATE app.users SET email = 'unknown@example.com';",
            "-- AlterColumnNullability",
        ]);
    }

    [Fact]
    public async Task Apply_TemplateMigration_FiresPerSchemaWhereTheChangeIsPlanned()
    {
        // A template with a migration applied to two schemas: sales.events already exists without the new column
        // (the migration matches and the add decomposes, with {schema} bound), while billing.events is brand new
        // (created empty, so its instance is unmatched and reports as inert).
        var current = new DatabaseSchema([
            new SchemaDefinition("sales", Tables: [new Table("events", Columns: [new Column("id", SqlType.Int)])]),
            new SchemaDefinition("billing"),
        ]);
        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA sales;
            CREATE SCHEMA billing;
            TEMPLATE audit
            BEGIN
                CREATE TABLE events
                (
                    id int NOT NULL,
                    actor text NOT NULL
                );
                MIGRATION 'backfill {schema} actors' FOR ADD COLUMN events.actor AS $$
                UPDATE {schema}.events SET actor = 'system';
                $$;
            END;
            APPLY TEMPLATE audit IN SCHEMA sales, billing;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire("apply", cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);
        var plan = result.Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Sql = plan.Sql! }, TestContext.Current.CancellationToken);

        // Sales decomposes around the token-substituted backfill; billing just creates its (empty) table.
        var statements = _executor.Executed.ShouldNotBeNull().Statements.Select(s => s.Sql).ToList();
        statements.ShouldBe([
            "-- CreateTable",
            "-- AddColumn",
            "UPDATE sales.events SET actor = 'system';",
            "-- AlterColumnNullability",
        ]);

        // Billing's instance is inert this run and says so; sales' matched instance reports nothing.
        var inert = result.Diagnostics.Where(d => d.Source == "data-migrations").ShouldHaveSingleItem();
        inert.Message.ShouldContain("'backfill billing actors'");
        inert.Message.ShouldContain("billing.events.actor");
    }

    [Fact]
    public async Task Apply_RunOnceScript_RunsOnce_ThenLaterPlansSkipIt()
    {
        // A run-once seed script: the first plan includes and records it, the next plan skips it.
        var current = new DatabaseSchema([new SchemaDefinition("app")]);
        var desired = WriteDdl("schema.sql",
            """
            CREATE SCHEMA app;
            SCRIPT 'seed currencies' RUN ONCE ON POST DEPLOYMENT AS $$
            INSERT INTO app.currencies VALUES ('GBP');
            $$;
            """);

        using var app = BuildApp(current, desired);

        // First run: the pending script is planned, executed, and recorded (the CLI-style flow threads
        // plan.RunOnceScripts into the apply).
        (await app.Locks.Acquire("apply", cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var first = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        first.Sql!.Statements.Select(s => s.Sql).ShouldContain("INSERT INTO app.currencies VALUES ('GBP');");
        first.Sql!.Scripts.ShouldHaveSingleItem().Name.ShouldBe("seed currencies");
        await app.Operations.Apply(new ApplyArguments { Sql = first.Sql! }, TestContext.Current.CancellationToken);

        _store.Written.ShouldNotBeNull().ExecutedScripts.ShouldHaveSingleItem().Name.ShouldBe("seed currencies");

        // Second run: the script is skipped, reported, and no longer up for recording.
        var second = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);
        second.Value!.Sql!.Statements.Select(s => s.Sql).ShouldNotContain("INSERT INTO app.currencies VALUES ('GBP');");
        second.Value!.Sql!.Scripts.ShouldBeEmpty();
        var skipped = second.Diagnostics.Where(d => d.Source == "run-once").ShouldHaveSingleItem();
        skipped.Message.ShouldContain("'seed currencies' has already run");
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
        _store.Written.ShouldNotBeNull().Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }
}
