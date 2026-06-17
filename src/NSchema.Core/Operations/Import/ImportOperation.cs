using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;

namespace NSchema.Operations.Import;

internal sealed class ImportOperation(ICurrentSchemaProvider currentSchema, IKeyedResolver<IOperationReporter> reporters) : IImportOperation
{
    public async Task Execute(ImportArguments arguments, CancellationToken cancellationToken = default)
    {
        reporters.Current.Info("Importing schema from database...");

        var schema = await currentSchema.GetSchema(SchemaSourceMode.Online, arguments.Schemas, cancellationToken: cancellationToken);

        if (arguments.Tables is { Length: > 0 })
        {
            schema = schema.FilterTables(arguments.Tables);
        }

        foreach (var (path, partition) in BuildPartitions(schema, arguments))
        {
            await WritePartition(path, partition, cancellationToken);
        }

        reporters.Current.Info("Schema imported successfully.");
    }

    private IEnumerable<(string path, DatabaseSchema schema)> BuildPartitions(DatabaseSchema schema, ImportArguments args)
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
                return schema.Schemas.Select(s => (Path.Combine(args.OutputDirectory, $"{s.Name}.sql"), new DatabaseSchema([s])));
            case ImportPartitionMode.Object:
                if (args.OutputDirectory is null)
                {
                    throw new InvalidOperationException("OutputDirectory must be set when Partition mode is Object.");
                }
                return schema.Schemas.SelectMany(s => ObjectPartitions(s, args.OutputDirectory));
            default:
                throw new InvalidOperationException($"Unknown partition mode: {args.Partition}");
        }
    }

    private static IEnumerable<(string path, DatabaseSchema schema)> ObjectPartitions(SchemaDefinition s, string directory)
    {
        // Each major object (table, view, function, procedure) gets its own file, grouped by type under
        // the schema's directory. The leftover schema-level objects (enums, sequences, grants, comment)
        // share a single per-schema "header" file alongside that directory. Splitting this way keeps any
        // one object out of every other object's file, so loading them all as desired-schema providers
        // never trips the aggregator's duplicate detection. Type subfolders also avoid collisions where a
        // table and a function share a name (they occupy distinct namespaces in the database).
        var header = s with { Tables = [], Views = [], Functions = [], Procedures = [] };
        yield return (Path.Combine(directory, $"{s.Name}.sql"), new DatabaseSchema([header]));

        foreach (var table in s.Tables)
        {
            yield return (Path.Combine(directory, s.Name, "tables", $"{table.Name}.sql"),
                new DatabaseSchema([new SchemaDefinition(s.Name, Tables: [table])]));
        }
        foreach (var view in s.Views)
        {
            yield return (Path.Combine(directory, s.Name, "views", $"{view.Name}.sql"),
                new DatabaseSchema([new SchemaDefinition(s.Name, Views: [view])]));
        }
        foreach (var function in s.Functions)
        {
            yield return (Path.Combine(directory, s.Name, "functions", $"{function.Name}.sql"),
                new DatabaseSchema([new SchemaDefinition(s.Name, Functions: [function])]));
        }
        foreach (var procedure in s.Procedures)
        {
            yield return (Path.Combine(directory, s.Name, "procedures", $"{procedure.Name}.sql"),
                new DatabaseSchema([new SchemaDefinition(s.Name, Procedures: [procedure])]));
        }
    }

    private async Task WritePartition(string path, DatabaseSchema incoming, CancellationToken cancellationToken)
    {
        var existing = await TryReadExisting(path, cancellationToken);
        var merged = existing is null ? incoming : Merge(existing, incoming);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ddl = DdlWriter.Instance.Write(merged);
        await File.WriteAllTextAsync(path, ddl, cancellationToken);
    }

    private async Task<DatabaseSchema?> TryReadExisting(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return DdlReader.Instance.Read(text).Schema;
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
                    Functions = PruneByName(s.Functions, i.Functions, f => f.Name),
                    Procedures = PruneByName(s.Procedures, i.Procedures, p => p.Name),
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
