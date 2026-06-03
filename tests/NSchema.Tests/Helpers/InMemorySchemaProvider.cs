using NSchema.Schema;
using NSchema.Schema.Model;

namespace NSchema.Tests.Helpers;

/// <summary>An <see cref="ISchemaProvider"/> that returns a fixed in-memory schema (honouring scope filtering).</summary>
internal sealed class InMemorySchemaProvider(DatabaseSchema schema) : ISchemaProvider
{
    public Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
        => Task.FromResult(schema.Filter(schemaNames));
}