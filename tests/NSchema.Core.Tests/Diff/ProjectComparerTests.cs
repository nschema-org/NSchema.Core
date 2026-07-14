using Microsoft.Extensions.Options;
using NSchema.State.Domain.Models;
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
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.Diff;

/// <summary>
/// The complete current→desired difference: run-once resolution and change-event matching compose with the
/// structural compare, so the diff carries the script runs it implies.
/// </summary>
public sealed class ProjectComparerTests
{
    private static readonly Database _emptySchema = new([]);
    private static readonly DatabaseDiff _emptyDiff = new([]);

    private readonly IDatabaseComparer _comparer = Substitute.For<IDatabaseComparer>();

    private ProjectComparer Sut => new(_comparer);

    public ProjectComparerTests()
    {
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(_emptyDiff);
    }

    private static Script SeedScript() =>
        new(new SqlIdentifier("seed"), new SqlText("INSERT INTO app.c VALUES (1);"), new DeploymentEvent(DeploymentPhase.Post)) { RunCondition = RunCondition.Once };

    private static Script EmailBackfillMigration() =>
        new(new SqlIdentifier("backfill_emails"), new SqlText("UPDATE app.users SET email = ''"), new ChangeEvent(ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email")) { ScopeSchema = new SqlIdentifier("app") });

    /// <summary>A current state recording <paramref name="sql"/> as <paramref name="script"/>'s executed body.</summary>
    private static CurrentState Executed(Script script, string sql) =>
        new(_emptySchema, [new ScriptExecution(script.Reference, ScriptHashing.Hash(new SqlText(sql)), DateTimeOffset.UnixEpoch)]);

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
        var current = new Database([new Schema(new SqlIdentifier("current"))]);
        var desired = new Database([new Schema(new SqlIdentifier("desired"))]);

        // Act
        Sut.Compare(new CurrentState(current), new ProjectDefinition(desired, []));

        // Assert
        _comparer.Received(1).Compare(current, desired, ProjectDirectives.Empty);
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
        var comparison = Sut.Compare(Executed(SeedScript(), SeedScript().Sql.Value), new ProjectDefinition(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        comparison.Require().IsEmpty.ShouldBeTrue();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_ExecutedRunOnceScriptWithChangedBody_StaysSkippedWithAWarning()
    {
        // Act — the recorded hash is of a different body; silently re-running is never safe.
        var comparison = Sut.Compare(Executed(SeedScript(), "some other body"), new ProjectDefinition(_emptySchema, [SeedScript()]));

        // Assert
        comparison.Require().Scripts.ShouldBeEmpty();
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Warning);
        diagnostic.Message.ShouldContain("'seed' has changed since it was executed");
    }

    [Fact]
    public void Compare_RunOnceExecutedUnderAnotherScope_StaysPending()
    {
        // Arrange — the ledger records (scope, name): an execution recorded for one schema's script does not
        // satisfy a global (or differently scoped) script sharing the name.
        var script = SeedScript();
        var scoped = new CurrentState(_emptySchema,
            [new ScriptExecution(new ScriptReference(new SqlIdentifier("sales"), script.Name), script.Hash, DateTimeOffset.UnixEpoch)]);

        // Act
        var comparison = Sut.Compare(scoped, new ProjectDefinition(_emptySchema, [script]));

        // Assert
        comparison.Require().Scripts.ShouldHaveSingleItem().ShouldBe(script);
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AlwaysScript_IgnoresRecordedExecutions()
    {
        // Arrange — same name recorded, but the script is not run-once.
        var script = SeedScript() with { RunCondition = RunCondition.Always };

        // Act
        var comparison = Sut.Compare(Executed(script, script.Sql.Value), new ProjectDefinition(_emptySchema, [script]));

        // Assert
        comparison.Require().Scripts.ShouldHaveSingleItem();
        comparison.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_MatchedMigration_AnnotatesTheNodeAndLandsInTheDiff()
    {
        // Arrange
        var migration = EmailBackfillMigration();
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(AddedEmailColumnDiff());

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
            "Migration 'app.backfill_emails' (ADD COLUMN app.users.email) matches no change in this plan and will " +
            "not run. If the change it supports has been applied everywhere, the block is safe to delete.");
    }

    [Fact]
    public void Compare_ExecutedRunOnceMigration_IsExcludedFromMatching()
    {
        // Arrange — the matching change IS in the diff, but the migration already ran: no annotation, no run.
        var migration = EmailBackfillMigration() with { RunCondition = RunCondition.Once };
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(AddedEmailColumnDiff());

        // Act
        var comparison = Sut.Compare(Executed(migration, migration.Sql.Value), new ProjectDefinition(_emptySchema, [migration]));

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
        _comparer.Compare(Arg.Any<Database>(), Arg.Any<Database>(), Arg.Any<ProjectDirectives>()).Returns(AddedEmailColumnDiff());

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
        var managed = new Database([new Schema(new SqlIdentifier("app"))]);

        // Act
        var diff = Sut.CompareTeardown(managed);

        // Assert
        _comparer.Received(1).Compare(managed, Arg.Is<Database>(d => d!.Schemas.Count == 0), ProjectDirectives.Empty);
        diff.Scripts.ShouldBeEmpty();
    }

    [Fact]
    public void Compare_AppliedRename_ReportsTheSpentDirective()
    {
        // Arrange — current has 'people' and no 'users': the rename has demonstrably been applied here.
        var current = new CurrentState(new Database([new Schema(new SqlIdentifier("app"),
            Tables: [new Table(new SqlIdentifier("people"))])]));
        var directives = new ProjectDirectives(Tables: new NSchema.Project.Domain.Models.Tables.TableDirectives(
            Renames: [new ObjectRename(new ObjectReference(new SqlIdentifier("app"), new SqlIdentifier("users")), new SqlIdentifier("people"))]));

        // Act
        var comparison = Sut.Compare(current, new ProjectDefinition(_emptySchema, [], directives));

        // Assert
        var diagnostic = comparison.Diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Info);
        diagnostic.Source.ShouldBe("directives");
        diagnostic.Message.ShouldContain("Rename of table 'app.users'");
        diagnostic.Message.ShouldContain("safe to delete");
    }

    [Fact]
    public void Compare_RenameOnAFreshDatabase_StaysSilent()
    {
        // Arrange — neither side of the rename exists (a fresh environment): the directive is pending, not
        // spent, so no expiry info fires.
        var directives = new ProjectDirectives(Tables: new NSchema.Project.Domain.Models.Tables.TableDirectives(
            Renames: [new ObjectRename(new ObjectReference(new SqlIdentifier("app"), new SqlIdentifier("users")), new SqlIdentifier("people"))]));

        // Act
        var comparison = Sut.Compare(new CurrentState(_emptySchema), new ProjectDefinition(_emptySchema, [], directives));

        // Assert
        comparison.Diagnostics.ShouldBeEmpty();
    }
}
