using NSchema.Operations.Confirmation;
using NSchema.Plan.Model;

namespace NSchema.Tests.Operations.Confirmation;

public sealed class OperationConfirmationRequestTests
{
    private readonly MigrationPlan _plan = new([], [], []);

    [Fact]
    public void ApplyConfirmationRequest_IsNotDestructive()
    {
        var request = new ApplyConfirmationRequest(_plan);

        request.IsDestructive.ShouldBeFalse();
        request.Plan.ShouldBe(_plan);
    }

    [Fact]
    public void DestroyConfirmationRequest_IsDestructive()
    {
        var request = new DestroyConfirmationRequest(_plan);

        request.IsDestructive.ShouldBeTrue();
        request.Plan.ShouldBe(_plan);
    }

    [Fact]
    public void Request_IsAssignableToBase()
    {
        OperationConfirmationRequest request = new DestroyConfirmationRequest(_plan);

        // A consumer reading only the base sees the semantic facts without a type switch.
        request.IsDestructive.ShouldBeTrue();
    }
}
