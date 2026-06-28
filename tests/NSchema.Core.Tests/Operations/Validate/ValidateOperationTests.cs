using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;
using NSchema.Operations.Validate;

namespace NSchema.Tests.Operations.Validate;

public sealed class ValidateOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();

    private ValidateOperation BuildSut() => new(_workflow, _progress);

    private readonly ValidateOperation _sut;

    public ValidateOperationTests()
    {
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(Result.Success());
        _sut = BuildSut();
    }

    [Fact]
    public async Task Execute_DelegatesToWorkflow_AndReturnsItsResult()
    {
        // Arrange
        var expected = Result.Failure(Diagnostic.Error("schema", "bad"));
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(expected);

        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert — the outcome is returned for the caller to render, not reported and thrown here.
        await _workflow.Received(1).Validate(Arg.Any<CancellationToken>());
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task Execute_WhenWorkflowThrows_Propagates()
    {
        // Arrange — a non-policy failure (e.g. unreadable schema files) still surfaces as an exception.
        _workflow.Validate(Arg.Any<CancellationToken>())
            .Returns<Result>(_ => throw new InvalidOperationException("boom"));

        // Act
        var act = () => _sut.Execute(new ValidateArguments());

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(act);
    }
}
