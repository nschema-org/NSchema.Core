using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Scripts;
using NSchema.Schema.Model.Templates;

namespace NSchema.Schema;

/// <summary>
/// The default <see cref="IDesiredSchemaProvider"/>: reads each registered <see cref="DdlSchemaSource"/>, reads the matched <c>.sql</c> files with <see cref="DdlReader"/>.
/// </summary>
internal sealed class DesiredSchemaProvider(IEnumerable<DdlSchemaSource> sources) : IDesiredSchemaProvider
{
    public async ValueTask<DesiredProject> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default)
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
        foreach (var document in documents)
        {
            schema = schema.Combine(document.Schema);
            scripts.AddRange(document.Scripts);
            templates = templates.Combine(document.Templates);
            AddMigrations(migrations, document.Migrations);
        }

        // Expand all templates.
        schema = TemplateExpander.Expand(schema, templates);

        // Filter by schema.
        schema = schema.Filter(schemaNames);
        if (schemaNames is not null)
        {
            migrations.RemoveAll(m => !schemaNames.Contains(m.SchemaName, StringComparer.OrdinalIgnoreCase));
        }

        return new DesiredProject(schema, scripts, migrations, files);
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

    private static async Task<DdlDocument> ReadDocument(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        try
        {
            return DdlReader.Instance.Read(text);
        }
        catch (DdlSyntaxException ex)
        {
            throw ex.WithSource(path);
        }
    }
}
