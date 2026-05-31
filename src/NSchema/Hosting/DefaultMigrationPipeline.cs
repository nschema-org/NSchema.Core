using System.Diagnostics;
using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>.
/// </summary>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="reporter">Presents user-facing migration progress and artifacts.</param>
/// <param name="stateCapturer">Captures the resulting schema into the state store after an apply.</param>
/// <param name="currentProvider">Supplies the online and offline current-state sources.</param>
/// <param name="desiredProviders">The providers that declare the desired database state.</param>
/// <param name="schemaAggregator">Merges multiple desired-state schemas into one.</param>
/// <param name="options">Migration options, including the schema-name scope filter.</param>
/// <param name="compiler">
/// Compiles the plan into an executable unit of work. Optional: an offline configuration (no database provider, so
/// no SQL is generated) has no compiler. A <see cref="Plan"/> then reports the plan without a SQL preview; an
/// <see cref="Apply"/> requires one and throws.
/// </param>
internal sealed class DefaultMigrationPipeline(
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    IStateCapturer stateCapturer,
    ICurrentSchemaProvider currentProvider,
    IEnumerable<ISchemaProvider> desiredProviders,
    ISchemaAggregator schemaAggregator,
    IOptions<MigrationOptions> options,
    // A default value makes this genuinely optional: MS DI only treats a parameter as optional when it has a
    // default, not from the nullable annotation alone. Without it, an offline run fails to construct the pipeline.
    IMigrationCompiler? compiler = null
) : IMigrationPipeline
{
    public async Task Plan(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");
        // Prefer the offline snapshot for planning so it works without a database connection.
        var source = currentProvider.GetSource(SchemaSourceMode.Offline, required: false);
        await Prepare(source, cancellationToken);
    }

    public async Task Apply(CancellationToken cancellationToken = default)
    {
        if (compiler is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to compile the plan into SQL, but none is registered. Register one (for example via UsePostgres).");
        }

        var source = currentProvider.GetSource(SchemaSourceMode.Online, required: true);

        // compiler is non-null here, so Prepare always produces an execution.
        var execution = await Prepare(source, cancellationToken) ?? throw new UnreachableException();

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

        // Capture the resulting schema so a later offline plan can diff against it. A no-op when no store
        // is configured; runs even for an empty diff to keep the state fresh.
        await stateCapturer.Capture(cancellationToken);
    }

    public async Task Refresh(CancellationToken cancellationToken = default)
    {
        if (!await stateCapturer.Capture(cancellationToken))
        {
            throw new InvalidOperationException("Refresh requires a state store. Register one via UseStateStore(...) or UseStateStoreFile(...).");
        }
    }

    private async Task<ICompiledMigration?> Prepare(ISchemaProvider source, CancellationToken cancellationToken)
    {
        reporter.Info("Computing migration plan...");

        // Collect and aggregate the desired schema from all registered providers.
        var scope = options.Value.SchemaNames;
        var schemas = await Task.WhenAll(desiredProviders.Select(p => p.GetSchema(scope, cancellationToken)));
        var desiredSchema = schemaAggregator.Aggregate(schemas);

        // Derive the scope for the current read when not explicitly set, so we only touch managed schemas.
        var schemasInScope = scope is { Length: > 0 }
            ? scope
            : desiredSchema.Schemas.Select(s => s.Name)
                .Concat(desiredSchema.DroppedSchemas)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var currentSchema = await source.GetSchema(schemasInScope, cancellationToken);

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
            return null;
        }

        reporter.Info("Compiling migration plan...");
        var execution = await compiler.Compile(result.Plan, cancellationToken);
        reporter.ReportPreview(execution.Preview);

        return execution;
    }
}
