namespace NSchema.Migration;

public interface ISqlPlanner
{
    SqlPlan Plan(SchemaPlan plan);
}
