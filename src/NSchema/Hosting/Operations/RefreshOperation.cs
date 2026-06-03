using NSchema.Migration;
using NSchema.State;

namespace NSchema.Hosting.Operations;

internal sealed class RefreshOperation(
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    ISchemaStateStore? store = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (store is null)
        {
            throw new InvalidOperationException("Unable to perform refresh without configured state store.");
        }

        reporter.Info("Refreshing schema state...");
        var schema = await currentProvider.GetSchema(SchemaSourceMode.Online, null, required: true, cancellationToken);
        await store.Write(schema, cancellationToken);
        reporter.Info("Schema state refreshed successfully.");
    }
}
