using Microsoft.Extensions.Options;
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
}
