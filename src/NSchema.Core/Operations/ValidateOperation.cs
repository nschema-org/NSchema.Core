using NSchema.Operations.Workflow;
using NSchema.Operations.Progress;

namespace NSchema.Operations;

/// <summary>
/// Loads the desired schema and validates it against the configured schema policies. Contacts no infrastructure.
/// </summary>
internal sealed class ValidateOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress)
    : IOperation<ValidateArguments, Result<ValidateResult>>
{
    public async Task<Result<ValidateResult>> Execute(ValidateArguments args, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Validating schema. No database or state store will be contacted."));

        var findings = await workflow.Validate(cancellationToken);
        return Result.Success(new ValidateResult(findings));
    }
}
