namespace NSchema.Migration;

public interface ISchemaMigrator
{
    Task<SchemaPlan> Plan(CancellationToken cancellationToken = default);
}
