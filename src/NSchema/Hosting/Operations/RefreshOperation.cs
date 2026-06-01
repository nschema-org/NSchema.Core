using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.State;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(
    IOptions<MigrationOptions> options,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    ISchemaStateStore? store = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            throw new InvalidOperationException("Refresh requires a state store. Register one via UseStateStore(...) or UseStateStoreFile(...).");
        }

        reporter.Info("Running in Refresh mode.");
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, options.Value.SchemaNames, required: true, cancellationToken);
        await store.Write(schema, cancellationToken);
        reporter.Info("Schema state captured.");
    }
}
