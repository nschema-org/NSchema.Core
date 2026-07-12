using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Project.Ddl;
using NSchema.Project.Ddl.Models;
using NSchema.Project.Ddl.Models.Templates;
using NSchema.Project.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project;

/// <summary>
/// The default <see cref="IProjectProvider"/>: reads each registered <see cref="ProjectSource"/>, reads the matched <c>.sql</c> files with <see cref="DdlReader"/>.
/// </summary>
internal sealed class ProjectProvider(IEnumerable<ProjectSource> sources) : IProjectProvider
{
    public async ValueTask<Result<ProjectDefinition>> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default)
    {
        var sourceList = sources.ToList();
        if (sourceList.Count == 0)
        {
            throw new InvalidOperationException("No SQL schema sources are registered.");
        }

        // Each source contributes its matched files in sorted order; sources stay in registration order (so an
        // environment overlay layers after the base). Reads fan out, then combine in that deterministic order so
        // duplicate detection and script ordering are stable.
        var files = sourceList.SelectMany(ResolveFiles).ToList();
        if (files.Count == 0)
        {
            return Result.Failure<ProjectDefinition>(ProjectDiagnostics.NoFilesMatched());
        }

        var documents = await Task.WhenAll(files.Select(file => ReadDocument(file, cancellationToken)));

        // Unreadable or unparseable files and cross-file duplicates are authoring mistakes — error diagnostics
        // on the result, all of them at once, with the best-effort merge of the readable files still carried.
        var diagnostics = new List<Diagnostic>();
        var schema = new DatabaseSchema();
        var scripts = new List<Script>();
        var templates = new TemplateSet();
        foreach (var document in documents)
        {
            diagnostics.AddRange(document.Diagnostics);
            if (document.Value is null)
            {
                continue;
            }
            var merged = SchemaAggregator.Combine(schema, document.Value.Schema);
            diagnostics.AddRange(merged.Diagnostics);
            schema = merged.Require();
            scripts.AddRange(document.Value.Scripts);
            templates = templates.Combine(document.Value.Templates);
        }

        // Apply all templates: application failures accumulate the same way.
        var applied = TemplateApplicator.Apply(new ProjectDefinition(schema, scripts), templates);
        diagnostics.AddRange(applied.Diagnostics);
        var project = applied.Require();

        // Collisions are project errors, so validate before scoping drops any instance — all of them at once.
        diagnostics.AddRange(ValidateChangeTargets(project.Scripts));
        diagnostics.AddRange(ValidateScriptNames(project.Scripts));

        // Filter by schema. Every script's scope comes off its event: hand-written deployment scripts are
        // global and survive any scope; template instances carry the schema they were applied for.
        var scoped = project.Schema.Filter(schemaNames);
        var scopedScripts = schemaNames is null
            ? project.Scripts
            : project.Scripts.Where(s => s.Event.ScopeSchema is not { } scope || schemaNames.Contains(scope, StringComparer.OrdinalIgnoreCase)).ToList();

        return Result.From(new ProjectDefinition(scoped, scopedScripts), diagnostics);
    }

    /// <summary>
    /// Enforces project-wide script-name uniqueness (after template expansion, so instantiated names count).
    /// Names identify scripts in run-once tracking and diagnostics, so a collision is an error, not a merge.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateScriptNames(IEnumerable<Script> scripts)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in scripts.Select(s => s.Name))
        {
            if (!names.Add(name))
            {
                yield return ProjectDiagnostics.DuplicateScriptName(name);
            }
        }
    }

    /// <summary>
    /// Rejects a second change-event script for the same trigger and path — two blocks preparing the same
    /// change is a conflict, whatever their names.
    /// </summary>
    private static IEnumerable<Diagnostic> ValidateChangeTargets(IEnumerable<Script> scripts)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var change in scripts.Select(s => s.Event).OfType<ChangeEvent>())
        {
            if (!targets.Add($"{change.Trigger}:{change.Path}"))
            {
                yield return ProjectDiagnostics.DuplicateChangeTarget(change);
            }
        }
    }

    private static IEnumerable<string> ResolveFiles(ProjectSource source) => source.Matcher
        .GetResultsInFullPath(source.BaseDirectory)
        .OrderBy(path => path, StringComparer.Ordinal);

    private static async Task<Result<DdlDocument>> ReadDocument(string path, CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(path, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result.Failure<DdlDocument>(ProjectDiagnostics.UnreadableFile(path, ex));
        }

        var read = DdlReader.Instance.Read(text);
        return read.IsSuccess
            ? read
            : Result.Failure<DdlDocument>(read.Diagnostics.Select(d => ProjectDiagnostics.InFile(path, d)));
    }
}
