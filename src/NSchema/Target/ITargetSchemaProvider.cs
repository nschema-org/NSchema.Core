using NSchema.Domain.Schema;

namespace NSchema.Target;

public interface ITargetSchemaProvider
{
    Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default);
}
