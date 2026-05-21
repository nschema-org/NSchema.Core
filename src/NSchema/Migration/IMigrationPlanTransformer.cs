namespace NSchema.Migration;

public interface IMigrationPlanTransformer
{
    SchemaPlan Transform(SchemaPlan plan);
}
