using NSchema.Migration.Plan;

namespace NSchema.Migration;

/// <summary>
/// Adapts a legacy <see cref="IMigrationExecutor"/> to <see cref="IMigrationCompiler"/>, so executors
/// registered via the obsolete <c>UseMigrationExecutor</c> extension point keep working.
/// </summary>
/// <remarks>
/// A legacy executor reports its work as a side effect of <see cref="IMigrationExecutor.Apply"/> rather
/// than returning it, so the compiled execution exposes an empty <see cref="ICompiledMigration.Preview"/>;
/// legacy executors therefore surface nothing in plan mode beyond the rendered diff. Apply behaviour is
/// preserved.
/// </remarks>
/// <param name="executor">The legacy executor to wrap.</param>
[Obsolete("Implement IMigrationCompiler directly instead. This adapter will be removed in a future major version.")]
internal sealed class ExecutorBackedCompiler(IMigrationExecutor executor) : IMigrationCompiler
{
    public Task<ICompiledMigration> Compile(MigrationPlan plan, CancellationToken cancellationToken = default)
        => Task.FromResult<ICompiledMigration>(new ExecutorBacked(executor, plan));

    private sealed class ExecutorBacked(IMigrationExecutor executor, MigrationPlan plan) : ICompiledMigration
    {
        public IReadOnlyList<string> Preview { get; } = [];

        public Task Execute(CancellationToken cancellationToken = default)
            => executor.Apply(plan, dryRun: false, cancellationToken);
    }
}

