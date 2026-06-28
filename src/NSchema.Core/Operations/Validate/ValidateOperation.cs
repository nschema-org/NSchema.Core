using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;

namespace NSchema.Operations.Validate;

/// <summary>
/// Loads the desired schema and validates it against the configured schema policies. Contacts no infrastructure.
/// </summary>
internal sealed class ValidateOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress)
    : IOperation<ValidateArguments, Result>
{
    public Task<Result> Execute(ValidateArguments args, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Validating schema. No database or state store will be contacted."));
        return workflow.Validate(cancellationToken);
    }
}
