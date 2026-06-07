using NSchema.Migration;
using NSchema.Operations.Services;
using NSchema.Resolution;

namespace NSchema.Operations;

internal sealed class ValidateOperation(IMigrationHelper helper, IKeyedResolver<IMigrationReporter> reporters) : INSchemaOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Validating schema. No database or state store will be contacted.");
        await helper.Validate(cancellationToken);
        reporters.Current.Info("Schema is valid.");
    }
}
