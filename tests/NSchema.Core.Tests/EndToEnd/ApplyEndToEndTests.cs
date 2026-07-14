using Microsoft.Extensions.DependencyInjection;
using NSchema.Apply;
using NSchema.Current.Backends;
using NSchema.Current.Locks;
using NSchema.Operations;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;

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

    private string WriteNsql(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplication BuildApp(DatabaseSchema current, string desiredPath) =>
        NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions())
            .AddProjectSource(Path.GetDirectoryName(desiredPath)!, Path.GetFileName(desiredPath))
            .UseStateStore(_store)
            .UseSqlDialect<StubSqlDialect>()
            .Tap(b =>
            {
                b.Services.AddSingleton<ISqlExecutor>(_executor);
                b.Services.AddSingleton<ISchemaIntrospector>(new InMemoryIntrospector(current));
            })
            .Build();

    [Fact]
    public async Task Apply_GeneratesSql_Executes_AndRefreshesState()
    {
        // Current live DB: an empty app schema. Desired: app.users(id) — i.e. create the table.
        var current = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"))]);
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = BuildApp(current, desired);

        // The CLI-style flow: hold the lock, compute the live plan, apply it, release.
        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // The plan reached the executor as a non-empty SQL plan.
        _executor.Executed.ShouldNotBeNull().ShouldNotBeEmpty();
        // The plan exposes the same SQL the caller previews before applying.
        plan.Plan!.Statements.ShouldBe(_executor.Executed!);
        // Post-apply state was captured to the store.
        ShouldlyIdentifierExtensions.ShouldBe(_store.Written.ShouldNotBeNull().Schema.Schemas.ShouldHaveSingleItem().Name, "app");
    }

    [Fact]
    public async Task Apply_RequiredColumnAddWithMatchedMigration_ExecutesNullableAddBackfillThenTighten()
    {
        // Current live DB: a populated-shaped app.users(id). Desired: the same table gaining a NOT NULL,
        // defaultless email column, with a SCRIPT block declaring the backfill.
        var current = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])]);
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL,
                email text NOT NULL
            );
            SCRIPT 'backfill emails' RUN ON ADD COLUMN app.users.email AS $$
            UPDATE app.users SET email = 'unknown@example.com';
            $$;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // The add was decomposed around the backfill: nullable add, the block's SQL, then the tighten — in order.
        var statements = _executor.Executed.ShouldNotBeNull().Select(s => s.Sql).ToList();
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
            new SchemaDefinition(new SqlIdentifier("sales"), Tables: [new Table(new SqlIdentifier("events"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]),
            new SchemaDefinition(new SqlIdentifier("billing")),
        ]);
        var desired = WriteNsql("schema.sql",
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
                SCRIPT 'backfill actors' RUN ON ADD COLUMN events.actor AS $$
                UPDATE {schema}.events SET actor = 'system';
                $$;
            END;
            APPLY TEMPLATE audit IN SCHEMA sales, billing;
            """);

        using var app = BuildApp(current, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);
        var plan = result.Value.ShouldNotBeNull();
        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // Sales decomposes around the token-substituted backfill; billing just creates its (empty) table.
        var statements = _executor.Executed.ShouldNotBeNull().Select(s => s.Sql).ToList();
        statements.ShouldBe([
            "-- CreateTable",
            "-- AddColumn",
            "UPDATE sales.events SET actor = 'system';",
            "-- AlterColumnNullability",
        ]);

        // Billing's instance is inert this run and says so; sales' matched instance reports nothing.
        var inert = result.Diagnostics.Where(d => d.Source == "data-migrations").ShouldHaveSingleItem();
        inert.Message.ShouldContain("'backfill actors'");
        inert.Message.ShouldContain("billing.events.actor");
    }

    [Fact]
    public async Task Apply_RunOnceScript_RunsOnce_ThenLaterPlansSkipIt()
    {
        // A run-once seed script: the first plan includes and records it, the next plan skips it.
        var current = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"))]);
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            SCRIPT 'seed currencies' RUN ONCE ON POST DEPLOYMENT AS $$
            INSERT INTO app.currencies VALUES ('GBP');
            $$;
            """);

        using var app = BuildApp(current, desired);

        // First run: the pending script is planned, executed, and recorded (the CLI-style flow threads
        // plan.RunOnceScripts into the apply).
        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var first = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        first.Plan!.Statements.Select(s => s.Sql).ShouldContain(new SqlText("INSERT INTO app.currencies VALUES ('GBP');"));
        ShouldlyIdentifierExtensions.ShouldBe(first.Plan!.Diff.Scripts.ShouldHaveSingleItem().Name, "seed currencies");
        await app.Operations.Apply(new ApplyArguments { Plan = first.Plan! }, TestContext.Current.CancellationToken);

        ShouldlyIdentifierExtensions.ShouldBe(_store.Written.ShouldNotBeNull().Scripts.ShouldHaveSingleItem().Script.Name, "seed currencies");

        // Second run: the script is skipped, and no longer up for recording.
        var second = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken);
        second.Value!.Plan!.Statements.Select(s => s.Sql).ShouldNotContain(new SqlText("INSERT INTO app.currencies VALUES ('GBP');"));
        second.Value!.Plan!.Diff.Scripts.ShouldBeEmpty();
        second.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public async Task Apply_WithNoChanges_ShortCircuitsWithoutExecutingButStillCapturesState()
    {
        var schema = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])]);
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = BuildApp(schema, desired);

        (await app.Locks.Acquire(new AcquireLockArguments("apply"), cancellationToken: TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var plan = (await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Live }, TestContext.Current.CancellationToken)).Value.ShouldNotBeNull();
        // The plan carries an empty diff/sql: there is nothing to apply.
        plan.Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
        plan.Plan!.IsEmpty.ShouldBeTrue();

        await app.Operations.Apply(new ApplyArguments { Plan = plan.Plan! }, TestContext.Current.CancellationToken);

        // Nothing to apply: the empty plan never reaches the executor...
        _executor.Executed.ShouldBeNull();
        // ...but a first run against an already-matching database still initialises the store.
        ShouldlyIdentifierExtensions.ShouldBe(_store.Written.ShouldNotBeNull().Schema.Schemas.ShouldHaveSingleItem().Name, "app");
    }
}
