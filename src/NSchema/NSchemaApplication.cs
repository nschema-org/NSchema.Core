using Microsoft.Extensions.Hosting;

namespace NSchema;

public class NSchemaApplication : IHost
{
    private bool _hasRun = false;
    private readonly IHost _host;

    internal NSchemaApplication(IHost host)
    {
        _host = host;
    }

    /// <inheritdoc />
    public IServiceProvider Services => _host.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_hasRun)
        {
            throw new InvalidOperationException("The application can only be started once.");
        }
        _hasRun = true;
        return _host.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _host.StopAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _host.Dispose();
    }

    public static NSchemaApplicationBuilder CreateBuilder() => new(new NSchemaApplicationOptions());
    public static NSchemaApplicationBuilder CreateBuilder(string[] args) => new(new NSchemaApplicationOptions { Args = args });
    public static NSchemaApplicationBuilder CreateBuilder(NSchemaApplicationOptions options) => new(options);
}
