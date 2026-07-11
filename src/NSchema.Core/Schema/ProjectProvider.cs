using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Diagnostics;
using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Templates;
using NSchema.Schema.Templates;

namespace NSchema.Schema;

/// <summary>
/// The default <see cref="IProjectProvider"/>: reads each registered <see cref="DdlSchemaSource"/>, reads the matched <c>.sql</c> files with <see cref="DdlReader"/>.
/// </summary>
internal sealed class ProjectProvider(IEnumerable<DdlSchemaSource> sources) : IProjectProvider
{
    public async ValueTask<Result<Project>> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default)
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
            throw new FileNotFoundException("No SQL DDL files matched the registered schema sources.");
        }

        var documents = await Task.WhenAll(files.Select(file => ReadDocument(file, cancellationToken)));

        var schema = new DatabaseSchema();
        var scripts = new List<Script>();
        var templates = new TemplateSet();
        foreach (var document in documents)
        {
            schema = schema.Combine(document.Schema);
            scripts.AddRange(document.Scripts);
            templates = templates.Combine(document.Templates);
        }

        // Expand all templates.
        var (expanded, instances) = TemplateExpander.Expand(schema, templates);
        schema = expanded;

        // Collisions are project errors, so validate before scoping drops any instance. They are expected
        // outcomes (authoring mistakes), so they ride the result as error diagnostics — all of them at once,
        // with the project still carried so the caller can see what it read.
        var diagnostics = new List<Diagnostic>();
        diagnostics.AddRange(ValidateChangeTargets(scripts.Concat(instances.Select(t => t.Script))));
        diagnostics.AddRange(ValidateScriptNames(scripts.Concat(instances.Select(t => t.Script))));

        // Filter by schema. A hand-written script's scope comes off its event (global scripts survive any
        // scope); a template instance scopes by the schema it was instantiated for — for scoped scripts that
        // equals their event's scope by construction, and for global ones it is the origin rule.
        schema = schema.Filter(schemaNames);
        if (schemaNames is not null)
        {
            scripts.RemoveAll(s => s.Event.ScopeSchema is { } scope && !schemaNames.Contains(scope, StringComparer.OrdinalIgnoreCase));
            instances = instances.Where(t => schemaNames.Contains(t.SchemaName, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        scripts.AddRange(instances.Select(t => t.Script));

        return Result.From(new Project(schema, scripts), diagnostics);
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
                yield return Diagnostic.Error("project",
                    $"Duplicate script name '{name}' declared. Script names must be unique across the project; " +
                    "a script declared in a template applied to multiple schemas can include the {schema} token in its name.");
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
                yield return Diagnostic.Error("project",
                    $"Duplicate migration for {ChangeEvent.TriggerText(change.Trigger)} '{change.Path}' declared.");
            }
        }
    }

    private static IEnumerable<string> ResolveFiles(DdlSchemaSource source) => source.Matcher
        .GetResultsInFullPath(source.BaseDirectory)
        .OrderBy(path => path, StringComparer.Ordinal);

    private static async Task<DdlDocument> ReadDocument(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return new DdlParser(text).Parse();
        }
        catch (DdlSyntaxException ex)
        {
            throw ex.WithSource(path);
        }
    }
}
