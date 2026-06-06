using Microsoft.Extensions.Options;
using NSchema.Import;
using NSchema.Migration;
using NSchema.Resolution;
using NSchema.Schema;

namespace NSchema.Hosting.Operations;

internal sealed class ImportOperation(
    IOptions<ImportOptions> options,
    ICurrentSchemaProvider currentSchema,
    IKeyedResolver<ISchemaImportTarget> targets,
    IKeyedResolver<IMigrationReporter> reporters
) : IMigrationOperation
{
    public async Task Execute(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        reporters.Current.Info("Importing schema from database...");

        var schema = await currentSchema.GetSchema(SchemaSourceMode.Online, opts.Schemas, cancellationToken: cancellationToken);

        if (opts.Tables is { Length: > 0 })
        {
            schema = schema.FilterTables(opts.Tables);
        }

        await targets.Current.Write(schema, cancellationToken);
        reporters.Current.Info("Schema imported successfully.");
    }
}
