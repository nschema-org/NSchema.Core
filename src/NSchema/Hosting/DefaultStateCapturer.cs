using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.State;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IStateCapturer"/>.
/// </summary>
/// <param name="options">Supplies the schema-name scope for the capture read.</param>
/// <param name="reporter">Presents capture progress.</param>
/// <param name="currentProvider">The current schema provider.</param>
/// <param name="store">The state store to write the snapshot to, if any.</param>
internal sealed class DefaultStateCapturer(
    IOptions<MigrationOptions> options,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    ISchemaStateStore? store = null
) : IStateCapturer
{
    public async Task<bool> Capture(CancellationToken cancellationToken = default)
    {
        if (store == null)
        {
            return false;
        }

        reporter.Info("Capturing schema state...");
        var schema = await currentProvider.GetSource(SchemaSourceMode.Online, required: true).GetSchema(options.Value.SchemaNames, cancellationToken);
        await store.Write(schema, cancellationToken);
        reporter.Info("Schema state captured.");
        return true;
    }
}
