using Microsoft.Extensions.Options;
using NSchema.Diff.Model;
using NSchema.Diff.Policies;
using NSchema.Plan.Model;
using NSchema.Policies;
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
        errors[0].Severity.ShouldBe(PolicyDiagnosticSeverity.Error);
        errors[0].PolicyName.ShouldBe("destructive-actions");
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
        results[0].Severity.ShouldBe(PolicyDiagnosticSeverity.Info);
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
        results[0].Severity.ShouldBe(PolicyDiagnosticSeverity.Warning);
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
        var view = new NSchema.Schema.Model.View("active_users", "SELECT * FROM app.users");
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
                Enums: [new EnumDiff("app", "status", ChangeKind.Add, Definition: new NSchema.Schema.Model.EnumType("status", ["a"]))],
                Sequences: [new SequenceDiff("app", "order_id", ChangeKind.Add, Definition: new NSchema.Schema.Model.Sequence("order_id"))]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_DroppedFunctionAndProcedure_AreDestructive()
    {
        // Arrange — dropping a routine loses its definition from managed state.
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var diff = new DatabaseDiff([
            new SchemaDiff("app",
                Functions: [new FunctionDiff("app", "f", ChangeKind.Remove)],
                Procedures: [new ProcedureDiff("app", "p", ChangeKind.Remove)]),
        ]);

        // Act
        var errors = _sut.Validate(diff).ToList();

        // Assert
        errors.ShouldHaveSingleItem();
        errors[0].Message.ShouldContain(nameof(DropFunction));
        errors[0].Message.ShouldContain(nameof(DropProcedure));
    }

    [Fact]
    public void Validate_FunctionSignatureRecreate_IsNotDestructive()
    {
        // Arrange — a signature change is a declared edit; the database blocks the underlying drop loudly if
        // dependents exist, so the policy does not gate it.
        _options.Value.Policy = DestructiveActionPolicy.Error;
        var fn = new NSchema.Schema.Model.Function("f", "a int, b text", "RETURNS int AS $$ SELECT 1 $$");
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Functions:
            [
                new FunctionDiff("app", "f", ChangeKind.Modify, Definition: fn,
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
            [new ExtensionDiff("citext", ChangeKind.Add, Definition: new NSchema.Schema.Model.Extension("citext"))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    private static DatabaseDiff TableChange(TableDiff table) =>
        new([new SchemaDiff("app", null, null, null, [], [table])]);
}
