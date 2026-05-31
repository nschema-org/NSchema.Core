using NSchema.Migration;
using NSchema.Schema;

namespace NSchema.Hosting.Operations;

internal static class SchemaResolution
{
    internal static async Task<(DatabaseSchema current, DatabaseSchema desired)> ResolveAsync(
        ISchemaProvider source,
        IDesiredSchemaProvider desiredProvider,
        string[]? schemaNames,
        CancellationToken cancellationToken)
    {
        var desiredSchema = await desiredProvider.GetSchema(schemaNames, cancellationToken);

        var schemasInScope = schemaNames is { Length: > 0 }
            ? schemaNames
            : desiredSchema.Schemas.Select(s => s.Name)
                .Concat(desiredSchema.DroppedSchemas)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        var currentSchema = await source.GetSchema(schemasInScope, cancellationToken);

        return (currentSchema, desiredSchema);
    }
}
