using Microsoft.Extensions.DependencyInjection;
using NSchema.Comparison;
using NSchema.Domain.Schema;
using NSchema.Migration;
using NSchema.Source;

namespace NSchema;

public sealed class NSchemaRunner
{
    private readonly IServiceProvider _services;
    private readonly DatabaseSchema _targetSchema;
    private readonly MigrationOptions _migrationOptions;

    internal NSchemaRunner(IServiceProvider services, DatabaseSchema targetSchema, MigrationOptions migrationOptions)
    {
        _services = services;
        _targetSchema = targetSchema;
        _migrationOptions = migrationOptions;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        var sourceProvider = _services.GetRequiredService<ISourceSchemaProvider>();
        var schemaNames = _targetSchema.Schemas.Select(s => s.Name).ToArray();
        var source = await sourceProvider.GetSchema(schemaNames, cancellationToken);

        var comparer = _services.GetRequiredService<ISchemaComparer>();
        var plan = comparer.Compare(source, _targetSchema);

        var migrator = _services.GetRequiredService<ISchemaMigrator>();
        await migrator.Migrate(plan, _migrationOptions, cancellationToken);
    }
}
