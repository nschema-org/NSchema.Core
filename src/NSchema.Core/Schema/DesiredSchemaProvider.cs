using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Diagnostics;
using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Templates;
using NSchema.Schema.Templates;

namespace NSchema.Schema;

/// <summary>
/// The default <see cref="IDesiredSchemaProvider"/>: reads each registered <see cref="DdlSchemaSource"/>, reads the matched <c>.sql</c> files with <see cref="DdlReader"/>.
/// </summary>
internal sealed class DesiredSchemaProvider(IEnumerable<DdlSchemaSource> sources) : IDesiredSchemaProvider
{
    public async ValueTask<DesiredProjectResult> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default)
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
        var migrations = new List<DataMigration>();
        var diagnostics = new List<Diagnostic>();
        foreach (var ((document, warnings), file) in documents.Zip(files))
        {
            schema = schema.Combine(document.Schema);
            scripts.AddRange(document.Scripts);
            templates = templates.Combine(document.Templates);
            AddMigrations(migrations, document.Migrations);
            diagnostics.AddRange(warnings.Select(w =>
                Diagnostic.Warning("deprecations", $"{w.Message} (at {w.Position} in {file}).")));
        }

        // Expand all templates.
        var (expanded, templateMigrations, templateScripts) = TemplateExpander.Expand(schema, templates);
        schema = expanded;
        AddMigrations(migrations, templateMigrations);

        // Name collisions are project errors, so validate before scoping drops any instance.
        ValidateScriptNames(scripts.Concat(templateScripts.Select(t => t.Script)), migrations);

        // Filter by schema. Template-instantiated scripts scope by their origin schema, like migrations;
        // hand-written deployment scripts are global and always survive.
        schema = schema.Filter(schemaNames);
        if (schemaNames is not null)
        {
            migrations.RemoveAll(m => !schemaNames.Contains(m.SchemaName, StringComparer.OrdinalIgnoreCase));
            templateScripts = templateScripts.Where(t => schemaNames.Contains(t.SchemaName, StringComparer.OrdinalIgnoreCase)).ToList();
        }
        scripts.AddRange(templateScripts.Select(t => t.Script));

        return new DesiredProjectResult(new DesiredProject(schema, scripts, migrations), files, diagnostics);
    }

    /// <summary>
    /// Enforces project-wide script-name uniqueness (after template expansion, so instantiated names count).
    /// Names identify scripts in run-once tracking and diagnostics, so a collision is an error, not a merge.
    /// </summary>
    private static void ValidateScriptNames(IEnumerable<Script> scripts, IReadOnlyList<DataMigration> migrations)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in scripts.Select(s => s.Name).Concat(migrations.Where(m => m.Name is not null).Select(m => m.Name!)))
        {
            if (!names.Add(name))
            {
                throw new InvalidOperationException(
                    $"Duplicate script name '{name}' declared. Script names must be unique across the project; " +
                    "a script declared in a template applied to multiple schemas can include the {schema} token in its name.");
            }
        }
    }

    private static void AddMigrations(List<DataMigration> migrations, IReadOnlyList<DataMigration> incoming)
    {
        foreach (var migration in incoming)
        {
            if (migrations.Any(m => m.Trigger == migration.Trigger && m.Path.Equals(migration.Path, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Duplicate migration for {DataMigration.TriggerText(migration.Trigger)} '{migration.Path}' declared.");
            }
            migrations.Add(migration);
        }
    }

    private static IEnumerable<string> ResolveFiles(DdlSchemaSource source) => source.Matcher
        .GetResultsInFullPath(source.BaseDirectory)
        .OrderBy(path => path, StringComparer.Ordinal);

    private static async Task<DdlParseResult> ReadDocument(string path, CancellationToken cancellationToken)
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
