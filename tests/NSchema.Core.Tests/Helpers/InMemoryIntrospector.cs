using NSchema.Deployment.Backends;
using NSchema.Model;

namespace NSchema.Tests.Helpers;

/// <summary>An <see cref="IDatabaseIntrospector"/> that returns a fixed in-memory schema (honouring scope filtering).</summary>
internal sealed class InMemoryIntrospector(Database schema) : IDatabaseIntrospector
{
    public ValueTask<Database> GetDatabase(DatabaseScope scope, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(scope.Apply(schema));
}
