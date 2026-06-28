using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Operations.Services;

namespace NSchema.Operations.Validate;

internal sealed class ValidateOperation(IMigrationWorkflow workflow, IProgress<OperationProgress> progress) : IValidateOperation
{
    public async Task<Result> Execute(ValidateArguments arguments, CancellationToken cancellationToken = default)
    {
        progress.Report(OperationProgress.Step("Validating schema. No database or state store will be contacted."));
        return await workflow.Validate(cancellationToken);
    }
}
