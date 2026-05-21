using NSchema.Schema;

namespace NSchema.Migration;

public interface ISchemaComparer
{
    SchemaPlan Compare(DatabaseSchema source, DatabaseSchema target);
}
