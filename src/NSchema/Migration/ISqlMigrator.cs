namespace NSchema.Migration;

public interface ISqlMigrator
{
    SqlPlan Plan(SchemaPlan plan);
    Task Apply(SqlPlan plan, CancellationToken cancellationToken = default);
}
