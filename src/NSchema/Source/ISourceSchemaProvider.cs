using NSchema.Domain.Schema;

namespace NSchema.Source;

public interface ISourceSchemaProvider
{
    Task<DatabaseSchema> GetSchema(string[] schemaNames, CancellationToken cancellationToken = default);
}
