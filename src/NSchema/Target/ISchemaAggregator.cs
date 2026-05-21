using NSchema.Domain.Schema;

namespace NSchema.Target;

public interface ISchemaAggregator
{
    DatabaseSchema Aggregate(IEnumerable<DatabaseSchema> schemas);
}
