using NSchema.Import;
using NSchema.Resolution;
using NSchema.Schema;

namespace NSchema.Operations.Import;

internal sealed class ImportOperation(
    ICurrentSchemaProvider currentSchema,
    IKeyedResolver<ISchemaImportTarget> targets,
    IKeyedResolver<IOperationReporter> reporters
) : IImportOperation
{
    public async Task Execute(ImportArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Importing schema from database...");

        var schema = await currentSchema.GetSchema(SchemaSourceMode.Online, arguments.Schemas, cancellationToken: cancellationToken);

        if (arguments.Tables is { Length: > 0 })
        {
            schema = schema.FilterTables(arguments.Tables);
        }

        await targets.Resolve(arguments.Target).Write(schema, cancellationToken);
        reporters.Current.Info("Schema imported successfully.");
    }
}
