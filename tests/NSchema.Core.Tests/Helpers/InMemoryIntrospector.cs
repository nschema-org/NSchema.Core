using NSchema.Current.Backends;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Helpers;

/// <summary>An <see cref="ISchemaIntrospector"/> that returns a fixed in-memory schema (honouring scope filtering).</summary>
internal sealed class InMemoryIntrospector(DatabaseSchema schema) : ISchemaIntrospector
{
    public ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(schema.Filter(schemaNames));
}
