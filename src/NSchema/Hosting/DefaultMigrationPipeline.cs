using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Policies;

namespace NSchema.Hosting;

/// <summary>
/// Default <see cref="IMigrationPipeline"/>. Owns all user-facing reporting and the
/// plan-then-execute orchestration. The planner and executor remain free of reporter concerns.
/// </summary>
/// <param name="options">The migration options.</param>
/// <param name="reporter">The reporter for user-facing migration progress.</param>
/// <param name="planRenderer">Renders the migration plan as a human-readable diff.</param>
/// <param name="planner">Builds the migration plan.</param>
/// <param name="compiler">Compiles the plan into an executable unit of work.</param>
internal sealed class DefaultMigrationPipeline(
    IOptions<MigrationOptions> options,
    IMigrationPlanner planner,
    IMigrationPlanRenderer planRenderer,
    IMigrationReporter reporter,
    IMigrationCompiler compiler
) : IMigrationPipeline
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var planOnly = options.Value.Operation == MigrationOperation.Plan;
        if (planOnly)
        {
            reporter.Info("Running in Plan mode. No changes will be applied to the database.");
        }

        reporter.Info("Computing migration plan...");
        Migration.Plan.MigrationPlan plan;
        try
        {
            plan = await planner.Plan(cancellationToken);
        }
        catch (PolicyViolationException ex)
        {
            reporter.Error("Validation failed:");
            foreach (var error in ex.Errors)
            {
                reporter.Error($"- {error.PolicyName}: {error.Message}");
            }
            throw;
        }

        reporter.Info(planRenderer.Render(plan) + Environment.NewLine);

        try
        {
            var execution = await compiler.Compile(plan, cancellationToken);
            foreach (var line in execution.Preview)
            {
                reporter.Info(line);
            }

            if (planOnly)
            {
                return;
            }

            reporter.Info("Running database migration...");
            await execution.Execute(cancellationToken);
            reporter.Info("Migration completed successfully.");
        }
        catch (Exception ex)
        {
            reporter.Error($"Migration failed: {ex.Message}");
            throw;
        }
    }
}
