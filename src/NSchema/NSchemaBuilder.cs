using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSchema.Comparison;
using NSchema.Domain.Schema;
using NSchema.Migration;
using NSchema.Target.Fluent;

namespace NSchema;

public sealed class NSchemaBuilder
{
    private readonly IServiceCollection _services = new ServiceCollection();
    private readonly MigrationOptions _migrationOptions = new();

    private DatabaseSchema? _targetSchema;

    public NSchemaBuilder()
    {
        _services.AddLogging(b => b.AddConsole());
    }

    public NSchemaBuilder ConfigureLogging(Action<ILoggingBuilder> configure)
    {
        _services.AddLogging(configure);
        return this;
    }

    public NSchemaBuilder UseTarget(DatabaseSchema schema)
    {
        _targetSchema = schema;
        return this;
    }

    public NSchemaBuilder UseTarget(Action<DatabaseModelBuilder> configure)
    {
        var builder = new DatabaseModelBuilder();
        configure(builder);
        _targetSchema = builder.Build();
        return this;
    }

    public NSchemaBuilder ConfigureOptions(Action<MigrationOptions> configure)
    {
        configure(_migrationOptions);
        return this;
    }

    public NSchemaBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        configure(_services);
        return this;
    }

    public NSchemaRunner Build()
    {
        if (_targetSchema is null)
        {
            throw new InvalidOperationException("A target schema must be configured via UseTarget.");
        }

        _services.TryAddSingleton<ISchemaComparer, DefaultSchemaComparer>();

        return new NSchemaRunner(_services.BuildServiceProvider(), _targetSchema, _migrationOptions);
    }
}
