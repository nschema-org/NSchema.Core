using NSchema.Resolution;
using NSchema.Schema;

namespace NSchema.Operations.Show;

internal sealed class ShowOperation(ICurrentSchemaProvider currentProvider, IOperationReporter reporter) : IShowOperation
{
    public async Task Execute(ShowArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Showing recorded state. The live database will not be contacted.");

        reporter.Progress("Reading recorded state...");
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);
        reporter.ReportSchema(recorded);
    }
}
