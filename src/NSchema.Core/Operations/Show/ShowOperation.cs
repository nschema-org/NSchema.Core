using NSchema.Resolution;
using NSchema.Schema;

namespace NSchema.Operations.Show;

internal sealed class ShowOperation(ICurrentSchemaProvider currentProvider, IKeyedResolver<IOperationReporter> reporters) : IShowOperation
{
    public async Task Execute(ShowArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Announce("Showing recorded state. The live database will not be contacted.");

        reporters.Current.Progress("Reading recorded state...");
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);
        reporters.Current.ReportSchema(recorded);
    }
}
