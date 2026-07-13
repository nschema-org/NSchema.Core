using NSchema.Project.Domain.Models;
using Microsoft.Extensions.Options;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Diff.Domain.Models.Extensions;
using NSchema.Diff.Domain.Models.Routines;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Sequences;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Views;
using NSchema.Plan.Domain.Models.Constraints;
using NSchema.Plan.Domain.Models.Enums;
using NSchema.Plan.Domain.Models.Extensions;
using NSchema.Plan.Domain.Models.Routines;
using NSchema.Plan.Domain.Models.Sequences;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Plan.Domain.Models.Views;
using NSchema.Plan.Policies;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Views;

namespace NSchema.Tests.Plan.Policies;

public class DestructiveActionPolicyTests
{
    private readonly IOptions<DestructiveActionOptions> _options = Options.Create(new DestructiveActionOptions());

    private readonly DestructiveActionPolicy _sut;

    public DestructiveActionPolicyTests()
    {
        _sut = new DestructiveActionPolicy(_options);
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsErrorForDestructiveAction()
    {
        // Arrange
        _options.Value.Policy = PolicyEnforcement.Error;

        // Act
        var errors = _sut.Validate(TestData.DestructiveDiff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Severity.ShouldBe(DiagnosticSeverity.Error);
        errors[0].Source.ShouldBe("destructive-actions");
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_WhenPolicyIsIgnore_ReturnsNothing()
    {
        // Arrange
        _options.Value.Policy = PolicyEnforcement.Ignore;

        // Act
        var results = _sut.Validate(TestData.DestructiveDiff);

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsAllow_ReturnsInfoDiagnostic()
    {
        // Arrange
        _options.Value.Policy = PolicyEnforcement.Allow;

        // Act
        var results = _sut.Validate(TestData.DestructiveDiff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(DiagnosticSeverity.Info);
    }

    [Fact]
    public void Validate_WhenPolicyIsWarn_ReturnsWarningDiagnostic()
    {
        // Arrange
        _options.Value.Policy = PolicyEnforcement.Warn;

        // Act
        var results = _sut.Validate(TestData.DestructiveDiff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
        results[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_NonDestructiveAction_ReturnsNothingRegardlessOfPolicy()
    {
        // Arrange
        _options.Value.Policy = PolicyEnforcement.Error;

        // Act
        var results = _sut.Validate(TestData.NonDestructiveDiff).ToList();

        // Assert
        results.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsOneErrorPerDestructiveActionType()
    {
        // Arrange
        var diff = TestData.DiffWithDroppedTables("users", "accounts");
        _options.Value.Policy = PolicyEnforcement.Error;

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Validate_DroppedUniqueConstraint_IsDestructive()
    {
        // Arrange — dropping a unique constraint removes a structural guarantee (and a possible FK target).
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = TableChange(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, null, null, [], [], [],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, new SqlIdentifier("users_email_uq"), null)]));

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropUniqueConstraint));
    }

    [Fact]
    public void Validate_DroppedExclusionConstraint_IsDestructive()
    {
        // Arrange — dropping an exclusion constraint removes a structural guarantee, like a unique constraint.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = TableChange(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("bookings"), ChangeKind.Modify, null, null, [], [], [],
            ExclusionConstraints: [new ExclusionConstraintDiff(ChangeKind.Remove, new SqlIdentifier("no_overlap"), null)]));

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropExclusionConstraint));
    }

    [Fact]
    public void Validate_DroppedCheckConstraint_IsNotDestructive()
    {
        // Arrange — dropping a check only loosens validation; no data is lost, so it is not destructive.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = TableChange(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, null, null, [], [], [],
            Checks: [new CheckConstraintDiff(ChangeKind.Remove, new SqlIdentifier("users_age_chk"), null)]));

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedView_IsDestructive()
    {
        // Arrange — dropping a view is destructive (its definition is lost from managed state).
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), null, null, null, [], [], [new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Remove)]),
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropView));
    }

    [Fact]
    public void Validate_AddedView_IsNotDestructive()
    {
        // Arrange — creating a view loses nothing.
        _options.Value.Policy = PolicyEnforcement.Error;
        var view = new View(new SqlIdentifier("active_users"), "SELECT * FROM app.users");
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), null, null, null, [], [], [new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Add, Definition: view)]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedEnum_IsDestructive()
    {
        // Arrange — dropping an enum is destructive (columns using it would lose their type definition).
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), Enums: [new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("status"), ChangeKind.Remove)]),
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropEnum));
    }

    [Fact]
    public void Validate_DroppedSequence_IsDestructive()
    {
        // Arrange — dropping a sequence loses its current position.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), Sequences: [new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("order_id"), ChangeKind.Remove)]),
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropSequence));
    }

    [Fact]
    public void Validate_AddedEnumAndSequence_AreNotDestructive()
    {
        // Arrange — creating an enum or sequence loses nothing.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"),
                Enums: [new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("status"), ChangeKind.Add, Definition: new EnumType(new SqlIdentifier("status"), ["a"]))],
                Sequences: [new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("order_id"), ChangeKind.Add, Definition: new Sequence(new SqlIdentifier("order_id")))]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedRoutines_AreDestructive()
    {
        // Arrange — dropping a routine loses its definition from managed state.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), Routines:
            [
                new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("f"), ChangeKind.Remove, RoutineKind.Function),
                new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("p"), ChangeKind.Remove, RoutineKind.Procedure),
            ]),
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropRoutine));
    }

    [Fact]
    public void Validate_FunctionSignatureRecreate_IsNotDestructive()
    {
        // Arrange — a signature change is a declared edit; the database blocks the underlying drop loudly if
        // dependents exist, so the policy does not gate it.
        _options.Value.Policy = PolicyEnforcement.Error;
        var fn = new Routine(new SqlIdentifier("f"), RoutineKind.Function, "a int, b text", "RETURNS int AS $$ SELECT 1 $$");
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), Routines:
            [
                new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("f"), ChangeKind.Modify, RoutineKind.Function, Definition: fn,
                    Arguments: new ValueChange<string>("a int", "a int, b text")),
            ]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedExtension_IsDestructive()
    {
        // Arrange — dropping a database-global extension removes shared infrastructure (and its dependents).
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff(Extensions: [new ExtensionDiff(new SqlIdentifier("citext"), ChangeKind.Remove)]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropExtension));
    }

    [Fact]
    public void Validate_AddedExtension_IsNotDestructive()
    {
        // Arrange — installing an extension loses nothing.
        _options.Value.Policy = PolicyEnforcement.Error;
        var diff = new DatabaseDiff(Extensions:
            [new ExtensionDiff(new SqlIdentifier("citext"), ChangeKind.Add, Definition: new Extension(new SqlIdentifier("citext")))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    private static DatabaseDiff TableChange(TableDiff table) =>
        new([new SchemaDiff(new SqlIdentifier("app"), null, null, null, [], [table])]);
}
