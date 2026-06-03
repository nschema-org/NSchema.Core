using NSchema.Hosting.Services;
using NSchema.Migration;
using NSchema.Migration.Sources;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IMigrationReporter reporter,
    IMigrationHelper helper,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Info("Planning schema migration. No changes will be applied to the database.");
        var plan = await helper.Prepare(SchemaSourceMode.Offline, required: false, cancellationToken);

        if (compiler == null)
        {
            reporter.Info("Unable to generate SQL preview. No provider is configured.");
        }
        else
        {
            reporter.Info("Compiling migration plan...");
            var execution = await compiler.Compile(plan, cancellationToken);
            reporter.ReportPreview(execution.Preview);
        }
    }
}
