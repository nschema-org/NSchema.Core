using NSchema.Migration.Plan;

namespace NSchema.Migration;

// This adapter exists solely to bridge the obsolete IMigrationExecutor to IMigrationCompiler, so
// disabling the obsolete-usage warning here is intentional.
#pragma warning disable CS0618 // Type or member is obsolete

/// <summary>
/// Adapts a legacy <see cref="IMigrationExecutor"/> to <see cref="IMigrationCompiler"/>, so executors
/// registered via the obsolete <c>UseMigrationExecutor</c> extension point keep working.
/// </summary>
/// <remarks>
/// A legacy executor reports its work as a side effect of <see cref="IMigrationExecutor.Apply"/> rather
/// than returning it, so the compiled execution exposes an empty <see cref="IMigrationExecution.Preview"/>;
/// legacy executors therefore surface nothing in plan mode beyond the rendered diff. Apply behaviour is
/// preserved.
/// </remarks>
/// <param name="executor">The legacy executor to wrap.</param>
internal sealed class ExecutorBackedCompiler(IMigrationExecutor executor) : IMigrationCompiler
{
    public Task<IMigrationExecution> Compile(MigrationPlan plan, CancellationToken cancellationToken = default)
        => Task.FromResult<IMigrationExecution>(new ExecutorBackedExecution(executor, plan));

    private sealed class ExecutorBackedExecution(IMigrationExecutor executor, MigrationPlan plan) : IMigrationExecution
    {
        public IReadOnlyList<string> Preview { get; } = [];

        public Task Execute(CancellationToken cancellationToken = default)
            => executor.Apply(plan, planOnly: false, cancellationToken);
    }
}

#pragma warning restore CS0618 // Type or member is obsolete
