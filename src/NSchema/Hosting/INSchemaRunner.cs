using NSchema.Migration;

namespace NSchema.Hosting;

public interface INSchemaRunner
{
    Task<SchemaPlan> Plan(CancellationToken cancellationToken = default);
    Task Apply(CancellationToken cancellationToken = default);
}
