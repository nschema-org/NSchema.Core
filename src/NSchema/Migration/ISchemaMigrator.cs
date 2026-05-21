namespace NSchema.Migration;

public interface ISchemaMigrator
{
    StatementPlan Plan(SchemaPlan plan);
    Task Apply(StatementPlan plan, CancellationToken cancellationToken = default);
}
