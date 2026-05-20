using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSchema.Execution;

namespace NSchema.Hosting;

public sealed class SchemaMigratorHostedService : IHostedService
{
    private readonly ISchemaMigrator _migrator;
    private readonly ExecutionOptions _options;
    private readonly ILogger<SchemaMigratorHostedService> _logger;

    public SchemaMigratorHostedService(
        ISchemaMigrator migrator,
        ExecutionOptions options,
        ILogger<SchemaMigratorHostedService> logger)
    {
        _migrator = migrator;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Running NSchema migration on startup.");
        await _migrator.Apply(_options, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
