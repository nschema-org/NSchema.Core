using NSchema.Schema;

namespace NSchema.Migration;

public interface ISchemaComparer
{
    SchemaPlan Compare(DatabaseSchema current, DatabaseSchema desired);
}
