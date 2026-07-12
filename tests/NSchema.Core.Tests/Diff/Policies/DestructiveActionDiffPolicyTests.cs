using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Diff.Domain.Models.Extensions;
using NSchema.Diff.Domain.Models.Routines;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Sequences;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Views;
using Microsoft.Extensions.Options;
using NSchema.Diff.Policies;
using NSchema.Diff.Domain.Models;
using NSchema.Plan.Domain.Models.Constraints;
using NSchema.Plan.Domain.Models.Enums;
using NSchema.Plan.Domain.Models.Extensions;
using NSchema.Plan.Domain.Models.Routines;
using NSchema.Plan.Domain.Models.Sequences;
using NSchema.Plan.Domain.Models.Tables;
using NSchema.Plan.Domain.Models.Views;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Extensions;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Views;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Diff.Policies;

public class DestructiveActionDiffPolicyTests
{
    private readonly IOptions<DestructiveActionOptions> _options = Options.Create(new DestructiveActionOptions());

    private readonly DestructiveActionDiffPolicy _sut;

    public DestructiveActionDiffPolicyTests()
    {
        _sut = new DestructiveActionDiffPolicy(_options);
    }

    [Fact]
    public void Validate_WhenPolicyIsError_ReturnsErrorForDestructiveAction()
    {
        // Arrange
        _options.Value.Policy = DestructiveActionPolicy.Error;

        // Act
        var errors = _sut.Validate(TestData.DestructiveDiff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Severity.ShouldBe(DiagnosticSeverity.Error);
        errors[0].Source.ShouldBe("destructive-actions");
        errors[0].Message.ShouldContain(nameof(DropTable));
    }

    [Fact]
    public void Validate_WhenPolicyIsAllow_ReturnsInfoDiagnostic()
    {
        // Arrange
        _options.Value.Policy = DestructiveActionPolicy.Allow;

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
        _options.Value.Policy = DestructiveActionPolicy.Warn;

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
        _options.Value.Policy = DestructiveActionPolicy.Error;

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
        _options.Value.Policy = DestructiveActionPolicy.Error;

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.Count.ShouldBe(1);
    }

    [Fact]
    public void Validate_DroppedUniqueConstraint_IsDestructive()
    {
        // Arrange — dropping a unique constraint removes a structural guarantee (and a possible FK target).
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = TableChange(new TableDiff("app", "users", ChangeKind.Modify, null, null, [], [], [],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Remove, "users_email_uq", null)]));

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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = TableChange(new TableDiff("app", "bookings", ChangeKind.Modify, null, null, [], [], [],
            ExclusionConstraints: [new ExclusionConstraintDiff(ChangeKind.Remove, "no_overlap", null)]));

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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = TableChange(new TableDiff("app", "users", ChangeKind.Modify, null, null, [], [], [],
            Checks: [new CheckConstraintDiff(ChangeKind.Remove, "users_age_chk", null)]));

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedView_IsDestructive()
    {
        // Arrange — dropping a view is destructive (its definition is lost from managed state).
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app", null, null, null, [], [], [new ViewDiff("app", "active_users", ChangeKind.Remove)]),
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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var view = new View("active_users", "SELECT * FROM app.users");
        var diff = new DatabaseDiff([
            new SchemaDiff("app", null, null, null, [], [], [new ViewDiff("app", "active_users", ChangeKind.Add, Definition: view)]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedEnum_IsDestructive()
    {
        // Arrange — dropping an enum is destructive (columns using it would lose their type definition).
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Enums: [new EnumDiff("app", "status", ChangeKind.Remove)]),
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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Sequences: [new SequenceDiff("app", "order_id", ChangeKind.Remove)]),
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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app",
                Enums: [new EnumDiff("app", "status", ChangeKind.Add, Definition: new EnumType("status", ["a"]))],
                Sequences: [new SequenceDiff("app", "order_id", ChangeKind.Add, Definition: new Sequence("order_id"))]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedRoutines_AreDestructive()
    {
        // Arrange — dropping a routine loses its definition from managed state.
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Routines:
            [
                new RoutineDiff("app", "f", ChangeKind.Remove, RoutineKind.Function),
                new RoutineDiff("app", "p", ChangeKind.Remove, RoutineKind.Procedure),
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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var fn = new Routine("f", RoutineKind.Function, "a int, b text", "RETURNS int AS $$ SELECT 1 $$");
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Routines:
            [
                new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function, Definition: fn,
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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff(Extensions: [new ExtensionDiff("citext", ChangeKind.Remove)]);

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
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff(Extensions:
            [new ExtensionDiff("citext", ChangeKind.Add, Definition: new Extension("citext"))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    private static DatabaseDiff TableChange(TableDiff table) =>
        new([new SchemaDiff("app", null, null, null, [], [table])]);
}
