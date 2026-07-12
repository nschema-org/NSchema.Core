using NSchema.Current.Backends;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Helpers;

/// <summary>An <see cref="ISchemaProvider"/> that returns a fixed in-memory schema (honouring scope filtering).</summary>
internal sealed class InMemorySchemaProvider(DatabaseSchema schema) : ISchemaProvider
{
    public ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(schema.Filter(schemaNames));
}
