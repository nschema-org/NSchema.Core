using Microsoft.Extensions.Options;
using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Diff.Model;
using NSchema.Diff.Policies;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Schemas;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;
using NSchema.Sql.Model;
using NSchema.State.Model;

namespace NSchema.Tests.Plan;

public sealed class MigrationPlannerTests
{
    private static readonly DatabaseSchema _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);
    private static readonly IReadOnlyList<Script> _noScripts = [];

    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();
    private readonly IPlanLinearizer _linearizer = Substitute.For<IPlanLinearizer>();
    private readonly List<ISchemaPolicy> _schemaPolicies = [];
    private readonly List<IDiffPolicy> _diffPolicies = [];

    private MigrationPlanner Sut => new(_comparer, _linearizer, _schemaPolicies, _diffPolicies);

    public MigrationPlannerTests()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(_emptyDiff);
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => []);
    }

    [Fact]
    public void Validate_RunsSchemaPoliciesAgainstDesiredSchema()
    {
        // Arrange
        var desired = new DatabaseSchema([new SchemaDefinition("app")]);
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(desired).Returns([Diagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var diagnostics = Sut.Validate(desired);

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
        policy.Received(1).Validate(desired);
    }

    [Fact]
    public void Plan_SchemaPolicyError_ShortCircuitsBeforeDiffing()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>()).Returns([Diagnostic.Error("Test", "bad schema")]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, []));

        // Assert: the schema stage is fatal — no planned migration at all (no diff, no plan).
        result.IsFailure.ShouldBeTrue();
        result.Value.ShouldBeNull();
        _comparer.DidNotReceive().Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>());
    }

    [Fact]
    public void Plan_NonFatalSchemaDiagnostics_FlowIntoResult()
    {
        // Arrange
        var policy = Substitute.For<ISchemaPolicy>();
        policy.Validate(Arg.Any<DatabaseSchema>())
            .Returns([new Diagnostic("Test", "lint", DiagnosticSeverity.Warning)]);
        _schemaPolicies.Add(policy);

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, []));

        // Assert: a non-error schema finding is carried through alongside the plan.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public void Plan_PassesBothSchemasToComparer()
    {
        // Arrange
        var current = new DatabaseSchema([new SchemaDefinition("current")]);
        var desired = new DatabaseSchema([new SchemaDefinition("desired")]);

        // Act
        Sut.Plan(new CurrentState(current), new DesiredProject(desired, _noScripts, []));

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public void Plan_AttachesPreAndPostDeploymentScriptsToPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns(_ => [coreAction]);
        IReadOnlyList<Script> scripts =
        [
            new Script("pre", "SELECT 1", ScriptType.PreDeployment),
            new Script("post", "SELECT 2", ScriptType.PostDeployment),
        ];

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, scripts, []));

        // Assert: scripts live on the plan (not interleaved into Actions, which carry only schema changes).
        result.Value.ShouldNotBeNull();
        result.Value!.Plan.Actions.ShouldHaveSingleItem().ShouldBe(coreAction);
        result.Value!.Plan.PreDeploymentScripts.ShouldBe([scripts[0]]);
        result.Value!.Plan.PostDeploymentScripts.ShouldBe([scripts[1]]);
    }

    [Fact]
    public void Plan_NoScripts_DoesNotAlterPlan()
    {
        // Arrange
        var coreAction = new CreateSchema("app");
        _linearizer.Linearize(Arg.Any<DatabaseDiff>())
            .Returns([coreAction]);

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, []));

        // Assert
        result.Value.ShouldNotBeNull();
        result.Value!.Plan.Actions.ShouldHaveSingleItem();
        result.Value!.Plan.Actions[0].ShouldBe(coreAction);
    }

    [Fact]
    public void Plan_RunsDiffPoliciesAgainstTheDiff()
    {
        // Arrange
        var policy = Substitute.For<IDiffPolicy>();
        policy.Validate(_emptyDiff).Returns([Diagnostic.Error("Test", "destructive")]);
        _diffPolicies.Add(policy);

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, []));

        // Assert
        result.Diagnostics.ShouldHaveSingleItem();
        result.Diagnostics[0].Message.ShouldBe("destructive");
        policy.Received(1).Validate(_emptyDiff);
    }

    [Fact]
    public void Plan_UnmatchedNamedMigration_YieldsOneDeadBlockInfoDiagnostic()
    {
        // Arrange — the comparer finds no changes, so the declared migration matches nothing.
        var migration = new DataMigration("backfill emails", DataMigrationTrigger.AddColumn, "app", "users", "email", "UPDATE 1");

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Source.ShouldBe("data-migrations");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Message.ShouldBe(
            "Migration 'backfill emails' (ADD COLUMN app.users.email) matches no change in this plan and will " +
            "not run. If the change it supports has been applied everywhere, the block is safe to delete.");
    }

    [Fact]
    public void Plan_UnmatchedAnonymousMigration_LabelsTheDiagnosticByTriggerAndPath()
    {
        // Arrange
        var migration = new DataMigration(null, DataMigrationTrigger.AddConstraint, "app", "users", "users_pk", "DELETE");

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Message.ShouldStartWith("Migration for ADD CONSTRAINT app.users.users_pk matches no change in this plan");
    }

    [Fact]
    public void Plan_MatchedMigration_YieldsNoDeadBlockDiagnostic()
    {
        // Arrange
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [EmailBackfillMigration()]));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_MatchedMigration_AnnotatesTheReturnedDiff()
    {
        // Arrange
        var migration = EmailBackfillMigration();
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert — the planned migration carries the annotated diff, not the comparer's raw one.
        result.Value.ShouldNotBeNull();
        result.Value!.Diff.Schemas[0].Tables[0].Columns[0].Migration.ShouldBe(migration);
    }

    [Fact]
    public void Plan_MatchedBackfill_SuppressesTheDataHazardWarning()
    {
        // Arrange — the real hazard policy sees the annotated diff, so a matched backfill silences the
        // NOT-NULL-add warning it would otherwise raise.
        _diffPolicies.Add(new DataHazardDiffPolicy(Options.Create(new DataHazardOptions())));
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var unmatched = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, []));
        var matched = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [EmailBackfillMigration()]));

        // Assert
        unmatched.Diagnostics.ShouldHaveSingleItem().Source.ShouldBe("data-hazards");
        matched.Diagnostics.ShouldBeEmpty();
    }

    /// <summary>A diff adding a required, defaultless <c>app.users.email</c> column to an existing table.</summary>
    private static DatabaseDiff AddedEmailColumnDiff() => new(
    [
        new SchemaDiff("app", Tables:
        [
            new TableDiff("app", "users", ChangeKind.Modify,
                Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))]),
        ]),
    ]);

    private static DataMigration EmailBackfillMigration() =>
        new("backfill emails", DataMigrationTrigger.AddColumn, "app", "users", "email", "UPDATE app.users SET email = ''");

    // -------------------------------------------------------------------------
    // Run-once scripts
    // -------------------------------------------------------------------------

    private static Script SeedScript() =>
        new("seed", "INSERT INTO app.c VALUES (1);", ScriptType.PostDeployment) { RunCondition = RunCondition.Once };

    /// <summary>A current state recording <paramref name="sql"/> as script <paramref name="name"/>'s executed body.</summary>
    private static CurrentState Executed(string name, string sql) =>
        new(_emptySchema, [new ScriptHash(name, ScriptHash.HashSql(sql))]);

    [Fact]
    public void Plan_PendingRunOnceScript_IsPlannedAndListedForRecording()
    {
        // Act — nothing recorded, so the script is part of the current→desired difference.
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, [SeedScript()], []));

        // Assert
        result.Value!.Plan.PostDeploymentScripts.ShouldHaveSingleItem().Name.ShouldBe("seed");
        var pending = result.Value!.Scripts.ShouldHaveSingleItem();
        pending.Name.ShouldBe("seed");
        pending.Hash.ShouldBe(ScriptHash.HashSql(SeedScript().Sql));
    }

    [Fact]
    public void Plan_ExecutedRunOnceScript_IsSkippedWithAnInfoDiagnostic()
    {
        // Act — the script has already run, so it is not part of the current→desired difference.
        var result = Sut.Plan(Executed("seed", SeedScript().Sql), new DesiredProject(_emptySchema, [SeedScript()], []));

        // Assert — dropped from the plan, reported, and not up for re-recording.
        result.Value!.Plan.PostDeploymentScripts.ShouldBeEmpty();
        result.Value!.Scripts.ShouldBeEmpty();
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Source.ShouldBe("run-once");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Message.ShouldContain("'seed' has already run");
    }

    [Fact]
    public void Plan_ExecutedRunOnceScriptWithChangedBody_StaysSkippedWithAWarning()
    {
        // Act — the recorded hash is of a different body; silently re-running is never safe.
        var result = Sut.Plan(Executed("seed", "some other body"), new DesiredProject(_emptySchema, [SeedScript()], []));

        // Assert
        result.Value!.Plan.PostDeploymentScripts.ShouldBeEmpty();
        var diagnostic = result.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("'seed' has changed since it was executed");
    }

    [Fact]
    public void Plan_AlwaysScript_IgnoresRecordedExecutions_AndIsNotListedForRecording()
    {
        // Arrange — same name recorded, but the script is not run-once.
        var script = SeedScript() with { RunCondition = RunCondition.Always };

        // Act
        var result = Sut.Plan(Executed("seed", script.Sql), new DesiredProject(_emptySchema, [script], []));

        // Assert
        result.Value!.Plan.PostDeploymentScripts.ShouldHaveSingleItem();
        result.Diagnostics.ShouldBeEmpty();
        result.Value!.Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Plan_ExecutedRunOnceMigration_IsExcludedFromMatching()
    {
        // Arrange — the matching change IS in the diff, but the migration already ran: no splice, no
        // annotation, and the hazard it would have suppressed surfaces again.
        _diffPolicies.Add(new DataHazardDiffPolicy(Options.Create(new DataHazardOptions())));
        var migration = EmailBackfillMigration() with { RunCondition = RunCondition.Once };
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var result = Sut.Plan(Executed("backfill emails", migration.Sql), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert
        result.Value!.Diff.Schemas[0].Tables[0].Columns[0].Migration.ShouldBeNull();
        result.Value!.Scripts.ShouldBeEmpty();
        result.Diagnostics.Select(d => d.Source).ShouldBe(["run-once", "data-hazards"]);
    }

    [Fact]
    public void Plan_MatchedRunOnceMigration_IsListedForRecording()
    {
        // Arrange
        var migration = EmailBackfillMigration() with { RunCondition = RunCondition.Once };
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert
        result.Value!.Scripts.ShouldHaveSingleItem().Name.ShouldBe("backfill emails");
    }

    [Fact]
    public void Plan_UnmatchedRunOnceMigration_IsNotListedForRecording()
    {
        // Arrange — empty diff, so the migration's event never occurs; it must not be recorded as run.
        var migration = EmailBackfillMigration() with { RunCondition = RunCondition.Once };

        // Act
        var result = Sut.Plan(new CurrentState(_emptySchema), new DesiredProject(_emptySchema, _noScripts, [migration]));

        // Assert — it still reports as inert, but stays pending.
        result.Value!.Scripts.ShouldBeEmpty();
        result.Diagnostics.ShouldHaveSingleItem().Source.ShouldBe("data-migrations");
    }

    [Fact]
    public void PlanTeardown_DiffsManagedSchemaAgainstEmpty()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);

        // Act
        Sut.PlanTeardown(managed);

        // Assert: the managed schema is diffed against an empty desired schema.
        _comparer.Received(1).Compare(managed, Arg.Is<DatabaseSchema>(d => d.Schemas.Count == 0 && d.DroppedSchemas.Count == 0));
    }

    [Fact]
    public void PlanTeardown_LinearizesTheDiff_WithoutDiagnostics()
    {
        // Arrange
        List<MigrationAction> actions = [new DropSchema("app")];
        _linearizer.Linearize(_emptyDiff).Returns(actions);

        // Act
        var result = Sut.PlanTeardown(new DatabaseSchema([new SchemaDefinition("app")]));

        // Assert
        result.Value!.Plan.Actions.ShouldBe(actions);
        result.Value!.Diff.ShouldBe(_emptyDiff);
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Count.ShouldBe(0);
    }

    [Fact]
    public void PlanTeardown_BypassesPolicies()
    {
        // Arrange
        var diffPolicy = Substitute.For<IDiffPolicy>();

        _diffPolicies.Add(diffPolicy);

        // Act
        Sut.PlanTeardown(new DatabaseSchema([new SchemaDefinition("app")]));

        // Assert
        diffPolicy.DidNotReceive().Validate(Arg.Any<DatabaseDiff>());
    }
}
