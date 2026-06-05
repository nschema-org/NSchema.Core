using Microsoft.Extensions.Options;
using NSchema.Resolution;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Import;

/// <summary>
/// An <see cref="ISchemaImportTarget"/> that writes the imported schema to the local filesystem, merging additively with any existing files.
/// </summary>
internal sealed class FileSchemaImportTarget(IOptions<FileSchemaImportTargetOptions> options, IKeyedResolver<ISchemaDocumentSerializer> serializers, ISchemaAggregator aggregator) : ISchemaImportTarget
{
    /// <summary>
    /// The name of the target, used for resolution.
    /// </summary>
    public const string TargetName = "file";

    public async Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        var serializer = serializers.Resolve(opts.Format);

        foreach (var (path, partition) in BuildPartitions(schema, opts))
        {
            await WritePartition(path, partition, serializer, cancellationToken);
        }
    }

    private static IEnumerable<(string path, DatabaseSchema schema)> BuildPartitions(DatabaseSchema schema, FileSchemaImportTargetOptions opts) => opts.Partition switch
    {
        ImportPartitionMode.None => [(opts.OutputPath, schema)],
        ImportPartitionMode.Schema => schema.Schemas.Select(s => (Path.Combine(opts.OutputPath, $"{s.Name}.{opts.Format}"), DatabaseSchema.Create([s]))),
        ImportPartitionMode.Table => schema.Schemas.SelectMany(s => s.Tables.Select(t => (Path.Combine(opts.OutputPath, s.Name, $"{t.Name}.{opts.Format}"), DatabaseSchema.Create([s with { Tables = [t] }])))),
        _ => throw new InvalidOperationException($"Unknown partition mode: {opts.Partition}")
    };

    private async Task WritePartition(string path, DatabaseSchema incoming, ISchemaDocumentSerializer serializer, CancellationToken cancellationToken)
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

    private static async Task<DatabaseSchema?> TryReadExisting(string path, ISchemaDocumentSerializer serializer, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await serializer.Read(stream, cancellationToken);
    }

    private DatabaseSchema Merge(DatabaseSchema existing, DatabaseSchema incoming)
    {
        // Strip any tables from existing that appear in the incoming import so the
        // aggregator sees no duplicates, then aggregate. Incoming tables win on conflict.
        var incomingTablesBySchema = incoming.Schemas.ToDictionary(
            s => s.Name,
            s => new HashSet<string>(s.Tables.Select(t => t.Name), StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        var prunedSchemas = existing.Schemas
            .Select(s => incomingTablesBySchema.TryGetValue(s.Name, out var incomingTables)
                ? s with { Tables = s.Tables.Where(t => !incomingTables.Contains(t.Name)).ToList() }
                : s)
            .ToList();

        var pruned = DatabaseSchema.Create(prunedSchemas, existing.DroppedSchemas.ToList());
        return aggregator.Aggregate([pruned, incoming]);
    }
}
