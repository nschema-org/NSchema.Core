using NSchema.Operations.Refresh;
using NSchema.Operations.Services;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Tests.Operations.Refresh;

public sealed class RefreshOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly RefreshOperation _sut;

    public RefreshOperationTests() => _sut = new RefreshOperation(_workflow);

    [Fact]
    public async Task Execute_WhenStateCaptured_ReturnsTheSchemaAndSnapshotSize()
    {
        // Arrange
        var schema = new DatabaseSchema([new SchemaDefinition("app")]);
        _workflow.Refresh(Arg.Any<CancellationToken>()).Returns(new StateCapture(schema, 2048));

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.CapturedSchema.ShouldBe(schema);
        result.Value!.SnapshotBytes.ShouldBe(2048);
    }

    [Fact]
    public async Task Execute_WhenNoStoreConfigured_Fails()
    {
        // Arrange — refresh's whole purpose is to capture to a store, so a missing store is a failure (not a no-op).
        _workflow.Refresh(Arg.Any<CancellationToken>()).Returns((StateCapture?)null);

        // Act
        var result = await _sut.Execute(new RefreshArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("without a configured state store");
    }
}
