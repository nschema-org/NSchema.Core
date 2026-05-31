using NSchema.Schema;

namespace NSchema.Migration;

internal sealed class DefaultDesiredSchemaProvider(IEnumerable<ISchemaProvider> providers, ISchemaAggregator aggregator) : IDesiredSchemaProvider
{
    public async Task<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var providerList = providers.ToList();
        if (providerList.Count == 0)
        {
            throw new InvalidOperationException("No schema providers are registered. Call AddSchema<T>() on the application builder to register at least one.");
        }

        var schemas = await Task.WhenAll(providerList.Select(p => p.GetSchema(schemaNames, cancellationToken)));
        return aggregator.Aggregate(schemas);
    }
}
