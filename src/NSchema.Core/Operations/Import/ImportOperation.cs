using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Operations.Import;

internal sealed class ImportOperation(
    ICurrentSchemaProvider currentSchema,
    IKeyedResolver<ISchemaSerializer> serializers,
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

        var serializer = serializers.Resolve(arguments.Format);

        foreach (var (path, partition) in BuildPartitions(schema, arguments))
        {
            await WritePartition(path, partition, serializer, cancellationToken);
        }

        reporters.Current.Info("Schema imported successfully.");
    }

    private static IEnumerable<(string path, DatabaseSchema schema)> BuildPartitions(DatabaseSchema schema, ImportArguments args)
    {
        switch (args.Partition)
        {
            case ImportPartitionMode.None:
                if (args.OutputFile is null)
                {
                    throw new InvalidOperationException("OutputFile must be set when Partition mode is None.");
                }
                return [(args.OutputFile, schema)];
            case ImportPartitionMode.Schema:
                if (args.OutputDirectory is null)
                {
                    throw new InvalidOperationException("OutputDirectory must be set when Partition mode is Schema.");
                }
                return schema.Schemas.Select(s => (Path.Combine(args.OutputDirectory, $"{s.Name}.{args.Format}"), new DatabaseSchema([s])));
            case ImportPartitionMode.Table:
                if (args.OutputDirectory is null)
                {
                    throw new InvalidOperationException("OutputDirectory must be set when Partition mode is Table.");
                }
                return schema.Schemas.SelectMany(s => s.Tables.Select(t => (Path.Combine(args.OutputDirectory, s.Name, $"{t.Name}.{args.Format}"),
                    new DatabaseSchema([s with { Tables = [t] }]))));
            default:
                throw new InvalidOperationException($"Unknown partition mode: {args.Partition}");
        }
    }

    private static async Task WritePartition(string path, DatabaseSchema incoming, ISchemaSerializer serializer, CancellationToken cancellationToken)
    {
        var existing = await TryReadExisting(path, serializer, cancellationToken);
        var merged = existing is null ? incoming : Merge(existing, incoming);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = File.Open(path, FileMode.Create, FileAccess.Write);
        await serializer.Write(merged, stream, cancellationToken);
    }

    private static async Task<DatabaseSchema?> TryReadExisting(string path, ISchemaSerializer serializer, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await serializer.Read(stream, cancellationToken);
    }

    private static DatabaseSchema Merge(DatabaseSchema existing, DatabaseSchema incoming)
    {
        // Strip any objects from existing that appear in the incoming import so the
        // aggregator sees no duplicates, then aggregate. Incoming objects win on conflict.
        var incomingBySchema = incoming.Schemas.ToDictionary(s => s.Name, s => s, StringComparer.OrdinalIgnoreCase);

        var prunedSchemas = existing.Schemas
            .Select(s => incomingBySchema.TryGetValue(s.Name, out var i)
                ? s with
                {
                    Tables = PruneByName(s.Tables, i.Tables, t => t.Name),
                    Views = PruneByName(s.Views, i.Views, v => v.Name),
                    Enums = PruneByName(s.Enums, i.Enums, e => e.Name),
                    Sequences = PruneByName(s.Sequences, i.Sequences, q => q.Name),
                }
                : s)
            .ToList();

        var pruned = new DatabaseSchema(prunedSchemas, existing.DroppedSchemas.ToList());
        return pruned.Combine(incoming);
    }

    private static List<T> PruneByName<T>(IReadOnlyList<T> existing, IReadOnlyList<T> incoming, Func<T, string> name)
    {
        var incomingNames = new HashSet<string>(incoming.Select(name), StringComparer.OrdinalIgnoreCase);
        return existing.Where(item => !incomingNames.Contains(name(item))).ToList();
    }
}
