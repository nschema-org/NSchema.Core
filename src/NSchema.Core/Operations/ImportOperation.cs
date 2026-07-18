using NSchema.Deployment;
using NSchema.Model;
using NSchema.Model.Schemas;
using NSchema.Operations.Progress;
using NSchema.Project.Model.Services;
using NSchema.Project.Nsql;

namespace NSchema.Operations;

/// <summary>
/// Reads the live schema and writes it out as desired-schema DDL source files, merging additively into any files that
/// already exist.
/// </summary>
internal sealed class ImportOperation(IDatabaseProvider database, IProgress<OperationProgress> progress)
    : IOperation<ImportArguments, Result<ImportResult>>
{
    public async Task<Result<ImportResult>> Execute(ImportArguments arguments, CancellationToken cancellationToken = default)
    {
        var diagnostics = new DiagnosticCollector();
        if (!diagnostics.TryTake(await database.GetDatabase(arguments.Scope, cancellationToken), out var schema))
        {
            return diagnostics.ToResult<ImportResult>(null);
        }
        progress.Report(OperationProgress.Detail($"Fetched {StatusHelpers.Describe(schema)} from the database."));

        // An existing file that cannot be read or parsed is skipped whole (never clobbered) and reported as an
        // error; the other files still import, and every broken file is reported at once.
        var written = new List<string>();

        async Task Import(string path, Database partition, bool declareSchemas)
        {
            var wrote = await WritePartition(path, partition, declareSchemas, cancellationToken);
            diagnostics.Add(wrote);
            if (wrote.IsSuccess)
            {
                written.Add(path);
            }
        }

        foreach (var definition in schema.Schemas)
        {
            var headerPath = Path.Combine(arguments.OutputDirectory, definition.Name.Value, "schema.sql");
            var header = definition.Clone();
            header.Tables.Clear();
            header.Views.Clear();
            header.Routines.Clear();
            await Import(headerPath, new Database { Schemas = [header] }, declareSchemas: true);

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
            await Import(path, new Database { Extensions = [.. schema.Extensions.Select(e => e.Clone())] }, declareSchemas: true);
        }

        return diagnostics.ToResult(new ImportResult(schema, written));
    }

    // Each major object (table, view, routine) gets its own file, grouped by type under the schema's directory;
    // the remaining schema-level objects (enums, sequences, grants, comment) share a per-schema header file in that directory.
    private static IEnumerable<(string path, Database schema)> ObjectPartitions(Schema s, string directory)
    {
        foreach (var table in s.Tables)
        {
            yield return (Path.Combine(directory, s.Name.Value, "tables", $"{table.Name}.sql"),
                new Database { Schemas = [new Schema { Name = s.Name, Tables = [table.Clone()] }] });
        }
        foreach (var view in s.Views)
        {
            yield return (Path.Combine(directory, s.Name.Value, "views", $"{view.Name}.sql"),
                new Database { Schemas = [new Schema { Name = s.Name, Views = [view.Clone()] }] });
        }
        // Functions and procedures share one name space, so they share one directory.
        foreach (var routine in s.Routines)
        {
            yield return (Path.Combine(directory, s.Name.Value, "routines", $"{routine.Name}.sql"),
                new Database { Schemas = [new Schema { Name = s.Name, Routines = [routine.Clone()] }] });
        }
    }

    private async Task<Result> WritePartition(string path, Database incoming, bool declareSchemas, CancellationToken cancellationToken)
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

        // The partition's document either declares its schemas (a header, the extensions file) or holds
        // member objects only — a property of the constructed document, not a rendering flag.
        var document = SyntaxBuilder.Build(merged, declareSchemas);
        var ddl = NsqlFormatter.Format(NsqlWriter.Write(document));
        await File.WriteAllTextAsync(path, ddl, cancellationToken);

        // Surface whether each object was created fresh or merged into an existing file — import is additive, so
        // this is the signal that an earlier import's file was updated in place rather than replaced.
        progress.Report(OperationProgress.Detail($"{(mergedIntoExisting ? "Merged into" : "Wrote")} {path}."));
        return Result.Success();
    }

    private static async Task<Result<Database>> ReadExisting(string path, CancellationToken cancellationToken)
    {
        var read = await NsqlReader.ReadFile(path, cancellationToken);
        if (read.IsFailure)
        {
            return Result.Failure<Database>(read.Diagnostics);
        }

        var assembled = Project.ProjectAssembler.Assemble([read.Value]);
        return Result.From(assembled.Require().Database, assembled.Diagnostics);
    }

    private static Database Merge(Database existing, Database incoming)
    {
        // Strip any objects from existing that appear in the incoming import so the
        // aggregator sees no duplicates, then aggregate. Incoming objects win on conflict.
        var incomingBySchema = incoming.Schemas.ToDictionary(s => s.Name, s => s);

        // The existing tree was just parsed from the file, so it is ours to prune in place.
        foreach (var s in existing.Schemas)
        {
            if (!incomingBySchema.TryGetValue(s.Name, out var i))
            {
                continue;
            }
            PruneByName(s.Tables, i.Tables, t => t.Name);
            PruneByName(s.Views, i.Views, v => v.Name);
            PruneByName(s.Enums, i.Enums, e => e.Name);
            PruneByName(s.Sequences, i.Sequences, q => q.Name);
            PruneByName(s.Routines, i.Routines, r => r.Name);
            PruneByName(s.Domains, i.Domains, d => d.Name);
        }

        // Root-level extensions are pruned by name too, so a re-imported extension merges in (incoming wins)
        // rather than tripping the aggregator's duplicate detection.
        PruneByName(existing.Extensions, incoming.Extensions, e => e.Name);

        // Pruning removed every overlapping object first, so this merge cannot collide.
        return DatabaseAggregator.Combine(existing, incoming).Require();
    }

    private static void PruneByName<T>(IList<T> existing, IReadOnlyList<T> incoming, Func<T, SqlIdentifier> name)
    {
        var incomingNames = incoming.Select(name).ToHashSet();
        existing.RemoveWhere(item => incomingNames.Contains(name(item)));
    }
}
