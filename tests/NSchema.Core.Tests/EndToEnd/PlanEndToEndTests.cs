using Microsoft.Extensions.DependencyInjection;
using NSchema.Deployment.Backends;
using NSchema.Diff.Model;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Schemas;
using NSchema.Model.Tables;
using NSchema.Operations;

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

    private string WriteNsql(string name, string content)
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, content);
        return path;
    }

    private NSchemaApplicationBuilder NewBuilder(Database current)
    {
        var builder = NSchemaApplication.CreateBuilder(new NSchemaApplicationOptions());
        builder.Services.AddSingleton<IDatabaseIntrospector>(new InMemoryIntrospector(current));
        // Planning requires a state store; nothing here needs to outlive the app, so an in-memory one suffices.
        builder.UseEphemeralState();
        return builder;
    }

    [Fact]
    public async Task Plan_ReportsStructuredDiffBetweenCurrentAndDesired()
    {
        // Current: app.users(id). Desired: app.users(id, email) + a new app.orders table.
        var current = new Database([new Schema(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])]);

        var desired = WriteNsql("schema.sql",
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

        // A plan diffs recorded state against the project, so the refresh is what puts the current schema on record.
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
        schema.Name.ShouldBe("app");

        var users = schema.Tables.Single(t => t.Name.Value.Equals("users"));
        users.Kind.ShouldBe(ChangeKind.Modify);
        users.Columns.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            c => c.Name.ShouldBe("email"),
            c => c.Kind.ShouldBe(ChangeKind.Add));

        schema.Tables.Single(t => t.Name.Value.Equals("orders")).Kind.ShouldBe(ChangeKind.Add);
    }

    [Fact]
    public async Task Plan_WithNoChanges_ReportsEmptyDiff()
    {
        var schema = new Database([new Schema(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])]);

        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(schema).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();

        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Plan_WithDialectRegistered_ProducesSql()
    {
        var current = new Database([]);
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current)
            .AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired))
            .UseSqlDialect<StubSqlDialect>()
            .Build();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Project }, TestContext.Current.CancellationToken);

        // The stub emits one statement per action; creating a schema yields a CreateSchema action.
        result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Statements.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Plan_WithoutProvider_Fails()
    {
        var current = new Database([]);
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).Build();

        var result = await app.Operations.Plan(new PlanArguments(), TestContext.Current.CancellationToken);

        // A dialect is required for planning — there is no SQL-less plan.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Message.Contains("Planning requires a database provider"));
    }

    [Fact]
    public async Task Plan_PartialTeardown_SeversWhatItCostsOutsideItsScope()
    {
        // A scoped teardown is the one plan that can be asked to remove something another schema depends on.
        // billing.orders keeps its rows; only the constraint aimed into app goes.
        var current = new Database(
        [
            new Schema(new SqlIdentifier("app"), Tables:
                [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])]),
            new Schema(new SqlIdentifier("billing"), Tables:
            [
                new Table(new SqlIdentifier("orders"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])
                {
                    ForeignKeys =
                    [
                        new ForeignKey(new SqlIdentifier("fk_orders_user"), [new SqlIdentifier("id")],
                            new SqlIdentifier("app"), new SqlIdentifier("users"), [new SqlIdentifier("id")]),
                    ],
                },
            ]),
        ]);
        var desired = WriteNsql("schema.sql", "CREATE SCHEMA app;");

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var result = await app.Operations.Plan(
            new PlanArguments { Target = PlanTarget.Empty, Scope = PlanningScope.Of(new SqlIdentifier("app")) },
            TestContext.Current.CancellationToken);

        var diff = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff;

        // app goes entirely...
        diff.Schemas.Single(s => s.Name.Value.Equals("app")).Kind.ShouldBe(ChangeKind.Remove);

        // ...and billing is disturbed, but not torn down: the table survives, minus the constraint.
        var billing = diff.Schemas.Single(s => s.Name.Value.Equals("billing"));
        billing.Kind.ShouldBeNull();
        var orders = billing.Tables.ShouldHaveSingleItem();
        orders.Kind.ShouldBe(ChangeKind.Modify);
        orders.ForeignKeys.ShouldHaveSingleItem().Kind.ShouldBe(ChangeKind.Remove);

        // And the reach outside the scope is announced rather than done quietly.
        result.Diagnostics.ShouldContain(d => d.Source == "scope" && d.Message.Contains("billing.orders.fk_orders_user"));
    }

    [Fact]
    public async Task Plan_Teardown_DiffsTheManagedSchemaDownToNothing()
    {
        // The managed schema is the recorded state, so the refresh records the live schema before tearing down.
        var current = new Database([new Schema(new SqlIdentifier("app"), Tables:
            [new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("id"), SqlType.Int)])])]);
        var desired = WriteNsql("schema.sql",
            """
            CREATE SCHEMA app;
            CREATE TABLE app.users
            (
                id int NOT NULL
            );
            """);

        using var app = NewBuilder(current).AddProjectSource(Path.GetDirectoryName(desired)!, Path.GetFileName(desired)).UseSqlDialect<StubSqlDialect>().Build();
        (await app.Operations.Refresh(new RefreshArguments(), TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();

        var result = await app.Operations.Plan(new PlanArguments { Target = PlanTarget.Empty }, TestContext.Current.CancellationToken);

        // A teardown is fully destructive, so the default policy blocks it — it must stay possible, but it does
        // not have to be easy. The block still carries the complete artifact, so review loses nothing.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldContain(d => d.Source == "destructive-actions");

        // The teardown plan drops the managed table.
        var schema = result.Value.ShouldNotBeNull().Plan.ShouldNotBeNull().Diff.Schemas.ShouldHaveSingleItem();
        schema.Tables.ShouldContain(t => t.Name.Value.Equals("users") && t.Kind == ChangeKind.Remove);
    }
}
