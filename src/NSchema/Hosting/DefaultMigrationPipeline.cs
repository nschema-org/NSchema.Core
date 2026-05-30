using System.Diagnostics;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>.
/// </summary>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="reporter">Presents user-facing migration progress and artifacts.</param>
/// <param name="stateCapturer">Captures the resulting schema into the state store after an apply.</param>
/// <param name="compiler">
/// Compiles the plan into an executable unit of work. Optional: an offline configuration (no database provider, so
/// no SQL is generated) has no compiler. A <see cref="Plan"/> then reports the plan without a SQL preview; an
/// <see cref="Apply"/> requires one and throws.
/// </param>
internal sealed class DefaultMigrationPipeline(
    IMigrationPlanner planner,
    IMigrationReporter reporter,
    IStateCapturer stateCapturer,
    // A default value makes this genuinely optional: MS DI only treats a parameter as optional when it has a
    // default, not from the nullable annotation alone. Without it, an offline run fails to construct the pipeline.
    IMigrationCompiler? compiler = null
) : IMigrationPipeline
{
    public async Task Plan(CancellationToken cancellationToken = default)
    {
        reporter.Info("Running in Plan mode. No changes will be applied to the database.");
        await Prepare(cancellationToken);
    }

    public async Task Apply(CancellationToken cancellationToken = default)
    {
        if (compiler is null)
        {
            throw new InvalidOperationException("Applying a migration requires a database provider to compile the plan into SQL, but none is registered. Register one (for example via UsePostgres).");
        }

        // compiler is non-null here, so Prepare always produces an execution.
        var execution = await Prepare(cancellationToken) ?? throw new UnreachableException();

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

    /// <summary>
    /// Computes the plan and presents the diff, then — when a compiler is configured — compiles it into an
    /// execution and presents the preview. Returns <see langword="null"/> for an offline run with no compiler.
    /// </summary>
    private async Task<ICompiledMigration?> Prepare(CancellationToken cancellationToken)
    {
        reporter.Info("Computing migration plan...");
        var result = await planner.Plan(cancellationToken);

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
