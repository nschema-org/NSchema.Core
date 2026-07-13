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
        var read = await currentSchema.GetSchema(SchemaSourceMode.Online, arguments.Scope, cancellationToken);
        if (read.Value is not { } schema)
        {
            return Result.Failure<ImportResult>(read.Diagnostics);
        }
        progress.Report(OperationProgress.Detail($"Fetched {StatusHelpers.Describe(schema)} from the database."));

        // An existing file that cannot be read or parsed is skipped whole (never clobbered) and reported as an
        // error; the other files still import, and every broken file is reported at once.
        var diagnostics = new List<Diagnostic>();
        var written = new List<string>();

        async Task Import(string path, DatabaseSchema partition, bool declareSchemas)
        {
            var wrote = await WritePartition(path, partition, declareSchemas, cancellationToken);
            diagnostics.AddRange(wrote.Diagnostics);
            if (wrote.IsSuccess)
            {
                written.Add(path);
            }
        }

        foreach (var definition in schema.Schemas)
        {
            var headerPath = Path.Combine(arguments.OutputDirectory, definition.Name, "schema.sql");
            await Import(headerPath, new DatabaseSchema([definition with { Tables = [], Views = [], Routines = [] }]), declareSchemas: true);

            foreach (var (path, partition) in ObjectPartitions(definition, arguments.OutputDirectory))
            {
                await Import(path, partition, declareSchemas: false);
            }
        }

        // Extensions are database-global, not schema-scoped, so they go to a single top-level file rather than
        // any per-schema directory.
        if (schema.Extensions.Count > 0)
        {
            var path = Path.Combine(arguments.OutputDirectory, "extensions.sql");
            await Import(path, new DatabaseSchema(Extensions: schema.Extensions), declareSchemas: true);
        }

        return Result.From(new ImportResult(schema, written), diagnostics);
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

    private async Task<Result> WritePartition(string path, DatabaseSchema incoming, bool declareSchemas, CancellationToken cancellationToken)
    {
        var merged = incoming;
        var mergedIntoExisting = false;
        if (File.Exists(path))
        {
            var existing = await ReadExisting(path, cancellationToken);
            if (existing.Value is not { } current)
            {
                return Result.From(existing.Diagnostics);
            }
            merged = Merge(current, incoming);
            mergedIntoExisting = true;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var ddl = DdlFormatter.Instance.Format(DdlWriter.Instance.Write(merged, declareSchemas));
        await File.WriteAllTextAsync(path, ddl, cancellationToken);

        // Surface whether each object was created fresh or merged into an existing file — import is additive, so
        // this is the signal that an earlier import's file was updated in place rather than replaced.
        progress.Report(OperationProgress.Detail($"{(mergedIntoExisting ? "Merged into" : "Wrote")} {path}."));
        return Result.Success();
    }

    private static async Task<Result<DatabaseSchema>> ReadExisting(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var read = DdlReader.Instance.Read(text);
        return read.IsSuccess
            ? read.Map(document => document.Schema)
            : Result.Failure<DatabaseSchema>(read.Diagnostics.Select(d => ProjectDiagnostics.InFile(path, d)));
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
