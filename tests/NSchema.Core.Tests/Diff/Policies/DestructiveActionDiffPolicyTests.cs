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

    private static DatabaseDiff TableChange(TableDiff table) =>
        new([new SchemaDiff("app", null, null, null, [], [table])]);
}
