using NSchema.Execution;

namespace NSchema.Hosting;

public interface ISchemaMigrator
{
    Task<MigrationPlan> Plan(CancellationToken cancellationToken = default);
    Task Apply(ExecutionOptions? options = null, CancellationToken cancellationToken = default);
}
