using NSchema.Hosting;
using NSchema.Operations;
using NSchema.Operations.Confirmation;
using NSchema.Plan.Model;

namespace NSchema.Tests.Hosting;

public sealed class AutoApproveConfirmationTests
{
    private readonly AutoApproveConfirmation _sut = new();

    [Fact]
    public async Task Confirm_ReturnsTrue()
    {
        // Arrange
        var request = new ApplyConfirmationRequest(new MigrationPlan([], [], []));

        // Act
        var result = await _sut.Confirm(request, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }
}
