using NSchema.Policies;

namespace NSchema.Tests.Policies;

public sealed class PolicyViolationExceptionTests
{
    [Fact]
    public void Message_IncludesErrorCount()
    {
        var errors = new[]
        {
            PolicyDiagnostic.Error("P1", "first"),
            PolicyDiagnostic.Error("P2", "second"),
        };

        var exception = new PolicyViolationException(errors);

        exception.Message.ShouldContain("2");
    }

    [Fact]
    public void Errors_ExposesTheSuppliedDiagnostics()
    {
        var errors = new[] { PolicyDiagnostic.Error("P1", "boom") };

        var exception = new PolicyViolationException(errors);

        exception.Errors.ShouldBe(errors);
    }

    [Fact]
    public void IsAnException() => new PolicyViolationException([]).ShouldBeAssignableTo<Exception>();
}
