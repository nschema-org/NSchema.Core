using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations.Validate;

internal sealed class ValidateOperation(IMigrationHelper helper, IKeyedResolver<IOperationReporter> reporters) : IValidateOperation
{
    public async Task Execute(ValidateArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Validating schema. No database or state store will be contacted.");
        await helper.Validate(cancellationToken);
        reporters.Current.Info("Schema is valid.");
    }
}
