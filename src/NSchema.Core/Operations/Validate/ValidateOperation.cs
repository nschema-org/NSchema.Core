using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations.Validate;

internal sealed class ValidateOperation(IMigrationWorkflow workflow, IOperationReporter reporter) : IValidateOperation
{
    public async Task Execute(ValidateArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Validating schema. No database or state store will be contacted.");
        await workflow.Validate(cancellationToken);
        reporter.Success("Schema is valid.");
    }
}
