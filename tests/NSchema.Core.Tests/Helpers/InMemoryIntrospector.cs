using NSchema.Deployment.Backends;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;

namespace NSchema.Tests.Helpers;

/// <summary>An <see cref="IDatabaseIntrospector"/> that returns a fixed in-memory schema (honouring scope filtering).</summary>
internal sealed class InMemoryIntrospector(Database schema) : IDatabaseIntrospector
{
    public ValueTask<Database> GetDatabase(SchemaScope scope, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(ScopeFilter.Apply(schema, scope));
}
