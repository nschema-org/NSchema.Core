using Microsoft.Extensions.Options;
using NSchema.Comparison;
using NSchema.Migration;
using NSchema.Source;
using NSchema.Target;

namespace NSchema.Hosting;

public sealed class DefaultNSchemaRunner(
    IOptions<MigrationOptions> options,
    ISourceSchemaProvider sourceProvider,
    IEnumerable<ITargetSchemaProvider> targetProviders,
    ISchemaAggregator schemaAggregator,
    ISchemaComparer comparer,
    ISchemaMigrator migrator
) : INSchemaRunner
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        // Get desired schema state from all registered providers and merge.
        var schemas = await Task.WhenAll(targetProviders.Select(p => p.GetSchema(cancellationToken)));
        var targetSchema = schemaAggregator.Aggregate(schemas);
        string[] schemasInScope = targetSchema.Schemas.Select(s => s.Name).ToArray();

        // Get current schema state.
        var source = await sourceProvider.GetSchema(schemasInScope, cancellationToken);

        // Diff the two schemas.
        var plan = comparer.Compare(source, targetSchema);

        // Migrate to the desired schema.
        await migrator.Migrate(plan, options.Value, cancellationToken);
    }
}
