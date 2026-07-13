using Microsoft.Extensions.Options;
using NSchema.Current.Domain.Models;
using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Plan.Policies;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Scripts;

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
        new(new SqlIdentifier("seed"), "INSERT INTO app.c VALUES (1);", new DeploymentEvent(DeploymentPhase.Post)) { RunCondition = RunCondition.Once };

    private static Script EmailBackfillMigration() =>
        new(new SqlIdentifier("backfill emails"), "UPDATE app.users SET email = ''", new ChangeEvent(ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email")) { ScopeSchema = new SqlIdentifier("app") });

    /// <summary>A current state recording <paramref name="sql"/> as script <paramref name="name"/>'s executed body.</summary>
    private static CurrentState Executed(string name, string sql) =>
        new(_emptySchema, [new ScriptExecution(new SqlIdentifier(name), ScriptHashing.Hash(sql), DateTimeOffset.UnixEpoch)]);

    /// <summary>A diff adding a required, defaultless <c>app.users.email</c> column to an existing table.</summary>
    private static DatabaseDiff AddedEmailColumnDiff() => new(
    [
        new SchemaDiff(new SqlIdentifier("app"), Tables:
        [
            new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify,
                Columns: [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]),
        ]),
    ]);

    [Fact]
    public void Compare_PassesBothSchemasToTheStructuralComparer()
    {
        // Arrange
        var current = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("current"))]);
        var desired = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("desired"))]);

        // Act
        Sut.Compare(new CurrentState(current), new ProjectDefinition(desired, []));

        // Assert
        _comparer.Received(1).Compare(current, desired);
    }

    [Fact]
    public void Compare_PendingDeploymentScript_LandsInTheDiff()
    {
        // Act — nothing recorded, so the script is part of the current→desired difference.
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldHaveSingleItem().Name.ShouldBe("seed");
        comparison.Require().IsEmpty.ShouldBeFalse();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScript_IsNotPartOfTheDifference()
    {
        // Act — the script has already run, so it is not part of the current→desired difference.
        var comparison = Sut.Compare(Executed("seed", SeedScript().Sql), new ProjectDefinition(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        comparison.Require().IsEmpty.ShouldBeTrue();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScriptWithChangedBody_StaysSkippedWithAWarning()
    {
        // Act — the recorded hash is of a different body; silently re-running is never safe.
        var comparison = Sut.Compare(Executed("seed", "some other body"), new ProjectDefinition(_emptySchema, [SeedScript()]));

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
        var comparison = Sut.Compare(Executed("seed", script.Sql), new ProjectDefinition(_emptySchema, [script]));

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
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, [migration]));

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
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, [migration]));

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
        var comparison = Sut.Compare(Executed("backfill emails", migration.Sql), new ProjectDefinition(_emptySchema, [migration]));

        // Assert
        comparison.Require().Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        comparison.Require().Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_MatchedBackfill_SuppressesTheDataHazardWarning()
    {
        // Arrange — the hazard policy sees the complete diff, so a matched backfill silences the NOT-NULL-add
        // warning it would otherwise raise.
        var policy = new DataHazardPolicy(Options.Create(new DataHazardOptions()));
        _comparer.Compare(Arg.Any<DatabaseSchema>(), Arg.Any<DatabaseSchema>()).Returns(AddedEmailColumnDiff());

        // Act
        var unmatched = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, []));
        var matched = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, [EmailBackfillMigration()]));

        // Assert
        policy.Validate(unmatched.Require()).ShouldHaveSingleItem().Source.ShouldBe("data-hazards");
        policy.Validate(matched.Require()).ShouldBeEmpty();
    }

    [Fact]
    public void CompareTeardown_DiffsManagedSchemaAgainstEmpty_WithNoScripts()
    {
        // Arrange
        var managed = new DatabaseSchema([new SchemaDefinition(new SqlIdentifier("app"))]);

        // Act
        var diff = Sut.CompareTeardown(managed);

        // Assert
        _comparer.Received(1).Compare(managed, Arg.Is<DatabaseSchema>(d => d.Schemas.Count == 0 && d.DroppedSchemas.Count == 0));
        diff.Scripts.ShouldBeEmpty();
    }
}
