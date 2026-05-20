using Microsoft.Extensions.Logging;
using NSchema.Diffing;
using NSchema.Domain.Schema;
using NSchema.Execution;
using NSchema.Extractors;

namespace NSchema.Hosting;

public sealed class SchemaMigrator : ISchemaMigrator
{
    private readonly ISchemaExtractor _extractor;
    private readonly ISchemaDiffer _differ;
    private readonly IInstructionExecutor _executor;
    private readonly DatabaseModel _desired;
    private readonly ILogger<SchemaMigrator> _logger;

    public SchemaMigrator(
        ISchemaExtractor extractor,
        ISchemaDiffer differ,
        IInstructionExecutor executor,
        DatabaseModel desired,
        ILogger<SchemaMigrator> logger)
    {
        _extractor = extractor;
        _differ = differ;
        _executor = executor;
        _desired = desired;
        _logger = logger;
    }

    public async Task<MigrationPlan> Plan(CancellationToken cancellationToken = default)
    {
        var current = await _extractor.Extract(cancellationToken);
        var instructions = _differ.Diff(current, _desired);
        return new MigrationPlan(instructions);
    }

    public async Task Apply(ExecutionOptions? options = null, CancellationToken cancellationToken = default)
    {
        var plan = await Plan(cancellationToken);

        if (plan.IsEmpty)
        {
            _logger.LogInformation("Schema is already up to date.");
            return;
        }

        _logger.LogInformation("Applying {Count} schema change(s).", plan.Instructions.Count);
        await _executor.Execute(plan.Instructions, options, cancellationToken);
    }
}
