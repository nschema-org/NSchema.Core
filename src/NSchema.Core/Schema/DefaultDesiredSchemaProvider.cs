using NSchema.Schema.Model;

namespace NSchema.Schema;

internal sealed class DefaultDesiredSchemaProvider(
    IEnumerable<ISchemaProvider> providers,
    ISchemaAggregator aggregator,
    IEnumerable<ISchemaTransformer> transformers
) : IDesiredSchemaProvider
{
    public async ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var providerList = providers.ToList();
        if (providerList.Count == 0)
        {
            throw new InvalidOperationException("No schema providers are registered.");
        }

        // Task.WhenAll has no ValueTask overload; materialize each call to a Task to fan out concurrently.
        List<DatabaseSchema> schemas = [];
        var schemaTasks = providerList.Select(provider => provider.GetSchema(schemaNames, cancellationToken));
        foreach (var schemaTask in schemaTasks)
        {
            schemas.AddRange(await schemaTask);
        }
        var aggregated = aggregator.Aggregate(schemas);
        return transformers.Aggregate(aggregated, (schema, transformer) => transformer.Transform(schema));
    }
}
