using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations.Validate;

internal sealed class ValidateOperation(IMigrationWorkflow workflow, IKeyedResolver<IOperationReporter> reporters) : IValidateOperation
{
    public async Task Execute(ValidateArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Announce("Validating schema. No database or state store will be contacted.");
        await workflow.Validate(cancellationToken);
        reporters.Current.Success("Schema is valid.");
    }
}
