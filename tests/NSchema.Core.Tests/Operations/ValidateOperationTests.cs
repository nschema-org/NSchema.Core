using NSchema.Operations.Workflow;
using NSchema.Operations;
using NSchema.Operations.Progress;
using NSchema.Policies;

namespace NSchema.Tests.Operations;

public sealed class ValidateOperationTests
{
    private readonly IMigrationWorkflow _workflow = Substitute.For<IMigrationWorkflow>();
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ValidateOperation _sut;

    public ValidateOperationTests()
    {
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(new PolicyDiagnostics());
        _sut = new ValidateOperation(_workflow, _progress);
    }

    [Fact]
    public async Task Execute_WhenSchemaClean_SucceedsWithNoFindings()
    {
        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.Findings.ShouldBeEmpty();
        result.Value!.HasErrors.ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_WhenSchemaInvalid_StillSucceedsAsAnOperation_ButFindingsHaveErrors()
    {
        // Arrange — finding problems is a successful validation, distinct from the validation itself failing to run.
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(new PolicyDiagnostics([Diagnostic.Error("P1", "bad schema")]));

        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasErrors.ShouldBeTrue();
        result.Value!.Errors.ShouldHaveSingleItem().Message.ShouldBe("bad schema");
    }

    [Fact]
    public async Task Execute_CarriesAdvisoryFindings_WithoutFlaggingErrors()
    {
        // Arrange
        _workflow.Validate(Arg.Any<CancellationToken>()).Returns(new PolicyDiagnostics([new Diagnostic("P1", "lint", DiagnosticSeverity.Warning)]));

        // Act
        var result = await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value!.HasErrors.ShouldBeFalse();
        result.Value!.Findings.ShouldHaveSingleItem().Message.ShouldBe("lint");
    }

    [Fact]
    public async Task Execute_ReportsTheValidatingStep()
    {
        // Act
        await _sut.Execute(new ValidateArguments(), TestContext.Current.CancellationToken);

        // Assert
        _progress.Received().Report(OperationProgress.Step("Validating schema. No database or state store will be contacted."));
    }
}
