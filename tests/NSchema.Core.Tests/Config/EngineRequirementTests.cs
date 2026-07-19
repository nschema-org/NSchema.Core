using NSchema.Config;

namespace NSchema.Tests.Config;

/// <summary>
/// The engine assertion: a host validates its own version against the project's ENGINE statement — the
/// engine never evaluates it implicitly.
/// </summary>
public sealed class EngineRequirementTests
{
    private readonly EngineRequirement _sut = new(VersionRange.Parse("[5.0,6.0)"));

    [Fact]
    public void Validate_VersionInRange_Passes()
        => _sut.Validate(SemanticVersion.Parse("5.2.1")).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Validate_VersionOutsideRange_Fails()
        => _sut.Validate(SemanticVersion.Parse("6.0.0")).Errors.ShouldHaveSingleItem()
            .ShouldBe(ConfigDiagnostics.EngineRequirementUnsatisfied(VersionRange.Parse("[5.0,6.0)"), SemanticVersion.Parse("6.0.0")));

    [Fact]
    public void Validate_BareRange_MeansExact()
    {
        // Arrange
        var sut = new EngineRequirement(VersionRange.Parse("5.1.0"));

        // Act & Assert
        sut.Validate(SemanticVersion.Parse("5.1.0")).IsSuccess.ShouldBeTrue();
        sut.Validate(SemanticVersion.Parse("5.1.1")).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void Validate_Prerelease_IsOutsideAReleaseOnlyRange()
        => _sut.Validate(SemanticVersion.Parse("5.0.0-alpha.1")).IsFailure.ShouldBeTrue();
}
