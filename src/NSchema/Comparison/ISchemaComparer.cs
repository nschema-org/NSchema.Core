using NSchema.Domain.Migration;
using NSchema.Domain.Schema;

namespace NSchema.Comparison;

public interface ISchemaComparer
{
    MigrationPlan Compare(DatabaseSchema source, DatabaseSchema target);
}
