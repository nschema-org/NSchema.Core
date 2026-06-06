using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Resolution;

namespace NSchema.Hosting.Operations;

internal sealed class ValidateOperation(IMigrationHelper helper, IKeyedResolver<IMigrationReporter> reporters) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Validating schema. No database or state store will be contacted.");
        await helper.Validate(cancellationToken);
        reporters.Current.Info("Schema is valid.");
    }
}
