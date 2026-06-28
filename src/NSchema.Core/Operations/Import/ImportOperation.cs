using NSchema.Diagnostics;
using NSchema.Operations.Progress;
using NSchema.Schema;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;

namespace NSchema.Operations.Import;

/// <summary>
/// Reads the live schema and writes it out as desired-schema DDL source files, merging additively into any files that
/// already exist.
/// </summary>
internal sealed class ImportOperation(ICurrentSchemaProvider currentSchema, IProgress<OperationProgress> progress)
    : IOperation<ImportArguments, Result<ImportResult>>
{
    public async Task<Result<ImportResult>> Execute(ImportArguments arguments, CancellationToken cancellationToken = default)
    {
        var schema = await currentSchema.GetSchema(SchemaSourceMode.Online, arguments.Schemas, cancellationToken: cancellationToken);
        progress.Report(OperationProgress.Detail($"Fetched {Census.Describe(schema)} from the database."));

        foreach (var (path, partition) in schema.Schemas.SelectMany(s => ObjectPartitions(s, arguments.OutputDirectory)))
        {
            await WritePartition(path, partition, cancellationToken);
        }

        // Extensions are database-global, not schema-scoped, so they go to a single top-level file rather than
        // any per-schema directory.
        if (schema.Extensions.Count > 0)
        {
            await WritePartition(
                Path.Combine(arguments.OutputDirectory, "extensions.sql"),
                new DatabaseSchema(Extensions: schema.Extensions),
                cancellationToken);
        }

        return Result.Success(new ImportResult());
    }

    // Each major object (table, view, routine) gets its own file, grouped by type under the schema's directory;
    // the remaining schema-level objects (enums, sequences, grants, comment) share a per-schema "header" file
    // alongside that directory.
    private static IEnumerable<(string path, DatabaseSchema schema)> ObjectPartitions(SchemaDefinition s, string directory)
    {
        var header = s with { Tables = [], Views = [], Routines = [] };
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
        // Functions and procedures share one name space, so they share one directory.
        foreach (var routine in s.Routines)
        {
            yield return (Path.Combine(directory, s.Name, "routines", $"{routine.Name}.sql"),
                new DatabaseSchema([new SchemaDefinition(s.Name, Routines: [routine])]));
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

        var ddl = DdlFormatter.Instance.Format(DdlWriter.Instance.Write(merged));
        await File.WriteAllTextAsync(path, ddl, cancellationToken);

        // Surface whether each object was created fresh or merged into an existing file — import is additive, so
        // this is the signal that an earlier import's file was updated in place rather than replaced.
        progress.Report(OperationProgress.Detail($"{(existing is null ? "Wrote" : "Merged into")} {path}."));
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
                    Routines = PruneByName(s.Routines, i.Routines, r => r.Name),
                    Domains = PruneByName(s.Domains, i.Domains, d => d.Name),
                }
                : s)
            .ToList();

        // Root-level extensions are pruned by name too, so a re-imported extension merges in (incoming wins)
        // rather than tripping the aggregator's duplicate detection.
        var pruned = new DatabaseSchema(
            prunedSchemas,
            existing.DroppedSchemas.ToList(),
            PruneByName(existing.Extensions, incoming.Extensions, e => e.Name),
            existing.DroppedExtensions.ToList()
        );
        return pruned.Combine(incoming);
    }

    private static List<T> PruneByName<T>(IReadOnlyList<T> existing, IReadOnlyList<T> incoming, Func<T, string> name)
    {
        var incomingNames = new HashSet<string>(incoming.Select(name), StringComparer.OrdinalIgnoreCase);
        return existing.Where(item => !incomingNames.Contains(name(item))).ToList();
    }
}
