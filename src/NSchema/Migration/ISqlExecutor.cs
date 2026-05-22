namespace NSchema.Migration;

public interface ISqlExecutor
{
    Task Execute(SqlPlan plan, CancellationToken cancellationToken = default);
}
