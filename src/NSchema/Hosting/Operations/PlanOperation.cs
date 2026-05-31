using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Hosting.Operations;

internal sealed class PlanOperation(
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    ICurrentSchemaProvider currentProvider,
    IEnumerable<ISchemaProvider> desiredProviders,
    ISchemaAggregator schemaAggregator,
    IOptions<MigrationOptions> options,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");

        var source = currentProvider.GetSource(SchemaSourceMode.Offline, required: false);
        var (currentSchema, desiredSchema) = await ResolveSchemas(source, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        reporter.ReportDiagnostics(result.Diagnostics);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);

        if (compiler is null)
        {
            reporter.Info("No database provider registered; reporting the plan without a SQL preview.");
            return;
        }

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);
    }

    private async Task<(DatabaseSchema current, DatabaseSchema desired)> ResolveSchemas(ISchemaProvider source, CancellationToken cancellationToken)
    {
        reporter.Info("Computing migration plan...");

        var scope = options.Value.SchemaNames;
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(scope, cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        var schemasInScope = scope is { Length: > 0 }
            ? scope
            : desiredSchema.Schemas.Select(s => s.Name)
                .Concat(desiredSchema.DroppedSchemas)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var currentSchema = await source.GetSchema(schemasInScope, cancellationToken);

        return (currentSchema, desiredSchema);
    }
}
