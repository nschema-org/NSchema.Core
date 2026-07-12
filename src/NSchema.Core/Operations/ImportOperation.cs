using NSchema.Current;
using NSchema.Operations.Progress;
using NSchema.Project.Ddl;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;

namespace NSchema.Operations;

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
        progress.Report(OperationProgress.Detail($"Fetched {StatusHelpers.Describe(schema)} from the database."));

        var written = new List<string>();
        foreach (var definition in schema.Schemas)
        {
            var headerPath = Path.Combine(arguments.OutputDirectory, definition.Name, "schema.sql");
            await WritePartition(headerPath, new DatabaseSchema([definition with { Tables = [], Views = [], Routines = [] }]), declareSchemas: true, cancellationToken);
            written.Add(headerPath);

            foreach (var (path, partition) in ObjectPartitions(definition, arguments.OutputDirectory))
            {
                await WritePartition(path, partition, declareSchemas: false, cancellationToken);
                written.Add(path);
            }
        }

        // Extensions are database-global, not schema-scoped, so they go to a single top-level file rather than
        // any per-schema directory.
        if (schema.Extensions.Count > 0)
        {
            var path = Path.Combine(arguments.OutputDirectory, "extensions.sql");
            await WritePartition(path, new DatabaseSchema(Extensions: schema.Extensions), declareSchemas: true, cancellationToken);
            written.Add(path);
        }

        return Result.Success(new ImportResult(schema, written));
    }

    // Each major object (table, view, routine) gets its own file, grouped by type under the schema's directory;
    // the remaining schema-level objects (enums, sequences, grants, comment) share a per-schema header file in that directory.
    private static IEnumerable<(string path, DatabaseSchema schema)> ObjectPartitions(SchemaDefinition s, string directory)
    {
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

    private async Task WritePartition(string path, DatabaseSchema incoming, bool declareSchemas, CancellationToken cancellationToken)
    {
        var existing = await TryReadExisting(path, cancellationToken);
        var merged = existing is null ? incoming : Merge(existing, incoming);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ddl = DdlFormatter.Instance.Format(DdlWriter.Instance.Write(merged, declareSchemas));
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
        try
        {
            return DdlReader.Instance.Read(text).Schema;
        }
        catch (DdlSyntaxException ex)
        {
            throw ex.WithSource(path);
        }
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
        // Pruning removed every overlapping object first, so this merge cannot collide.
        return SchemaAggregator.Combine(pruned, incoming).Require();
    }

    private static List<T> PruneByName<T>(IReadOnlyList<T> existing, IReadOnlyList<T> incoming, Func<T, string> name)
    {
        var incomingNames = new HashSet<string>(incoming.Select(name), StringComparer.OrdinalIgnoreCase);
        return existing.Where(item => !incomingNames.Contains(name(item))).ToList();
    }
}
