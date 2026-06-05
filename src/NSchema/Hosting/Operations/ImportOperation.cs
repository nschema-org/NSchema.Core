using Microsoft.Extensions.Options;
using NSchema.Import;
using NSchema.Schema;

namespace NSchema.Hosting.Operations;

internal sealed class ImportOperation(
    IOptions<ImportOptions> options,
    ICurrentSchemaProvider currentSchema,
    ISchemaImportTargetResolver target,
    IMigrationReporterResolver reporter
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        if (!target.TryForTarget(options.Value.Target, out var importTarget))
        {
            throw new InvalidOperationException("Import target could not be found.");
        }

        var opts = options.Value;
        reporter.Current.Info("Importing schema from database...");

        var schema = await currentSchema.GetSchema(SchemaSourceMode.Online, opts.Schemas, cancellationToken: cancellationToken);

        if (opts.Tables is { Length: > 0 })
        {
            schema = schema.FilterTables(opts.Tables);
        }

        await importTarget.Write(schema, cancellationToken);
        reporter.Current.Info("Schema imported successfully.");
    }
}
