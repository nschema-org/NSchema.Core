using NSchema.Hosting;
using NSchema.Plan.Model;

namespace NSchema.Tests.Hosting;

public sealed class AutoApproveConfirmationTests
{
    private readonly AutoApproveConfirmation _sut = new();

    [Fact]
    public async Task Confirm_ReturnsTrue()
    {
        // Arrange
        var plan = new MigrationPlan([], [], []);

        // Act
        var result = await _sut.Confirm(plan, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeTrue();
    }
}
