using NSchema.Schema;

namespace NSchema.Migration;

internal sealed class DefaultDesiredSchemaProvider(IEnumerable<ISchemaProvider> providers, ISchemaAggregator aggregator) : IDesiredSchemaProvider
{
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var schemas = await Task.WhenAll(providers.Select(p => p.GetSchema(schemaNames, cancellationToken)));
        return aggregator.Aggregate(schemas);
    }
}
