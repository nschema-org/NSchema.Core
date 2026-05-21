namespace NSchema.Hosting;

public interface INSchemaRunner
{
    Task Run(CancellationToken cancellationToken = default);
}
