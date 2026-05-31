using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;
using NSchema.Schema;

namespace NSchema.Hosting.Operations;

internal sealed class ApplyOperation(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    IStateCapturer stateCapturer,
    ICurrentSchemaProvider currentProvider,
    IEnumerable<ISchemaProvider> desiredProviders,
    ISchemaAggregator schemaAggregator,
    IMigrationCompiler? compiler = null
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (compiler is null)
        {
            throw new InvalidOperationException(
                "Applying a migration requires a database provider to compile the plan into SQL, but none is registered. Register one (for example via UsePostgres).");
        }

        var source = currentProvider.GetSource(SchemaSourceMode.Online, required: true);
        var (currentSchema, desiredSchema) = await ResolveSchemas(source, cancellationToken);

        var result = await planner.Plan(currentSchema, desiredSchema, cancellationToken);

        reporter.ReportDiagnostics(result.Diagnostics);
        if (result.HasErrors)
        {
            throw new PolicyViolationException(result.Errors.ToList());
        }

        reporter.ReportPlan(result.Plan);

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);

        try
        {
            reporter.Info("Running database migration...");
            await execution.Execute(cancellationToken);
            reporter.Info("Migration completed successfully.");
        }
        catch (Exception ex)
        {
            reporter.Error($"Migration failed: {ex.Message}");
            throw;
        }

        // Capture the resulting schema so a later offline plan can diff against it.
        // A no-op when no store is configured; runs even for an empty diff to keep the state fresh.
        await stateCapturer.Capture(cancellationToken);
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
