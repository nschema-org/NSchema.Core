using NSchema.Diff;
using NSchema.Resolution;
using NSchema.Schema;

namespace NSchema.Operations.Drift;

internal sealed class DriftOperation(
    ICurrentSchemaProvider currentProvider,
    IKeyedResolver<IOperationReporter> reporters,
    ISchemaComparer comparer
) : IDriftOperation
{
    public async Task Execute(DriftArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Announce("Checking for drift between the recorded state and the live database...");
        reporters.Current.Progress("Reading recorded state...");
        var recorded = await currentProvider.GetSchema(SchemaSourceMode.Offline, arguments.Schemas, required: true, cancellationToken);

        reporters.Current.Progress("Reading live database...");
        var live = await currentProvider.GetSchema(SchemaSourceMode.Online, arguments.Schemas, required: true, cancellationToken);

        // Diff direction: recorded -> live, so the changes describe how the live database has drifted from
        // what we recorded (an added object appears as Add, an out-of-band drop as Remove). This is a pure
        // observation: no transformers or policies run, so it never fails on policy violations.
        var diff = comparer.Compare(recorded, live);

        reporters.Current.ReportDiff(diff);

        if (diff.IsEmpty)
        {
            reporters.Current.Success("No drift detected.");
        }
        else
        {
            reporters.Current.Warn("Drift detected.");
        }
    }
}
