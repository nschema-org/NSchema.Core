using Microsoft.Extensions.Options;
using NSchema.Diagnostics;
using NSchema.Diff;
using NSchema.Schema;
using NSchema.Diff.Model;
using NSchema.Diff.Policies;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Tests.Diff;

/// <summary>
/// The complete current→desired difference: run-once resolution and change-event matching compose with the
/// structural compare, so the diff carries the script runs it implies.
/// </summary>
public sealed class ProjectComparerTests
{
    private static readonly DatabaseSchema _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);

    private readonly ISchemaComparer _comparer = Substitute.For<ISchemaComparer>();

    private ProjectComparer Sut => new(_comparer);

    public ProjectComparerTests()
    {
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(_emptyDiff);
    }

    private static Script SeedScript() =>
        new("seed", "INSERT INTO app.c VALUES (1);", new DeploymentEvent(DeploymentPhase.Post)) { RunCondition = RunCondition.Once };

    private static Script EmailBackfillMigration() =>
        new("backfill emails", "UPDATE app.users SET email = ''", new ChangeEvent(ChangeTrigger.AddColumn, "app", "users", "email"));

    /// <summary>A current state recording <paramref name="sql"/> as script <paramref name="name"/>'s executed body.</summary>
    private static CurrentState Executed(string name, string sql) =>
        new(_emptySchema, [new ScriptExecution(name, ScriptHashing.Hash(sql), DateTimeOffset.UnixEpoch)]);

    /// <summary>A diff adding a required, defaultless <c>app.users.email</c> column to an existing table.</summary>
    private static DatabaseDiff AddedEmailColumnDiff() => new(
    [
        new SchemaDiff("app", Tables:
        [
            new TableDiff("app", "users", ChangeKind.Modify,
                Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))]),
        ]),
    ]);

    [Fact]
    public void Compare_PassesBothSchemasToTheStructuralComparer()
    {
        // Arrange
        var current = new DatabaseSchema([new SchemaDefinition("current")]);
        var desired = new DatabaseSchema([new SchemaDefinition("desired")]);

        // Act
        Sut.Compare(new CurrentState(current), new Project(desired, []));

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public void Compare_PendingDeploymentScript_LandsInTheDiff()
    {
        // Act — nothing recorded, so the script is part of the current→desired difference.
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldHaveSingleItem().Name.ShouldBe("seed");
        comparison.Require().IsEmpty.ShouldBeFalse();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScript_IsNotPartOfTheDifference()
    {
        // Act — the script has already run, so it is not part of the current→desired difference.
        var comparison = Sut.Compare(Executed("seed", SeedScript().Sql), new Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        comparison.Require().IsEmpty.ShouldBeTrue();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScriptWithChangedBody_StaysSkippedWithAWarning()
    {
        // Act — the recorded hash is of a different body; silently re-running is never safe.
        var comparison = Sut.Compare(Executed("seed", "some other body"), new Project(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("'seed' has changed since it was executed");
    }

    [Fact]
    public void Compare_AlwaysScript_IgnoresRecordedExecutions()
    {
        // Arrange — same name recorded, but the script is not run-once.
        var script = SeedScript() with { RunCondition = RunCondition.Always };

        // Act
        var comparison = Sut.Compare(Executed("seed", script.Sql), new Project(_emptySchema, [script]));

        // Assert
        comparison.Require().Scripts.ShouldHaveSingleItem();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_MatchedMigration_AnnotatesTheNodeAndLandsInTheDiff()
    {
        // Arrange
        var migration = EmailBackfillMigration();
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new Project(_emptySchema, [migration]));

        // Assert — the node references the script by name; the script itself lives once, on the diff's root list.
        comparison.Require().Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration.Name);
        comparison.Require().Scripts.ShouldBe([migration]);
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_UnmatchedMigration_StaysOutOfTheDiff_WithADeadBlockDiagnostic()
    {
        // Arrange — the comparer finds no changes, so the declared migration matches nothing.
        var migration = EmailBackfillMigration();

        // Act
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new Project(_emptySchema, [migration]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Source.ShouldBe("data-migrations");
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Message.ShouldBe(
            "Migration 'backfill emails' (ADD COLUMN app.users.email) matches no change in this plan and will " +
            "not run. If the change it supports has been applied everywhere, the block is safe to delete.");
    }

    [Fact]
    public void Compare_ExecutedRunOnceMigration_IsExcludedFromMatching()
    {
        // Arrange — the matching change IS in the diff, but the migration already ran: no annotation, no run.
        var migration = EmailBackfillMigration() with { RunCondition = RunCondition.Once };
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var comparison = Sut.Compare(Executed("backfill emails", migration.Sql), new Project(_emptySchema, [migration]));

        // Assert
        comparison.Require().Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        comparison.Require().Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_MatchedBackfill_SuppressesTheDataHazardWarning()
    {
        // Arrange — the hazard policy sees the complete diff, so a matched backfill silences the NOT-NULL-add
        // warning it would otherwise raise.
        var policy = new DataHazardDiffPolicy(Options.Create(new DataHazardOptions()));
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var unmatched = Sut.Compare(new CurrentState(_emptySchema), new Project(_emptySchema, []));
        var matched = Sut.Compare(new CurrentState(_emptySchema), new Project(_emptySchema, [EmailBackfillMigration()]));

        // Assert
        policy.Validate(unmatched.Require()).ShouldHaveSingleItem().Source.ShouldBe("data-hazards");
        policy.Validate(matched.Require()).ShouldBeEmpty();
    }

    [Fact]
    public void CompareTeardown_DiffsManagedSchemaAgainstEmpty_WithNoScripts()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition("app")]);

        // Act
        var diff = Sut.CompareTeardown(managed);

        // Assert
        _comparer.Received(1).Compare(managed, Arg.Is<DatabaseSchema>(d => d.Schemas.Count == 0 && d.DroppedSchemas.Count == 0));
        diff.Scripts.ShouldBeEmpty();
    }
}
