using Microsoft.Extensions.FileSystemGlobbing;
using NSchema.Schema.Ddl;
using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model;
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
        var templates = new List<TemplateDefinition>();
        var applications = new List<TemplateApplication>();
        var includes = new List<TemplateInclude>();
        foreach (var document in documents)
        {
            schema = schema.Combine(document.Schema);
            scripts.AddRange(document.Scripts);
            templates.AddRange(document.Templates);
            applications.AddRange(document.Applications);
            includes.AddRange(document.Includes);
        }

        schema = TemplateExpander.Expand(schema, templates, applications, includes);
        schema = schema.Filter(schemaNames);

        return new DesiredProject(schema, scripts, files);
    }

    private static IEnumerable<string> ResolveFiles(DdlSchemaSource source) => source.Matcher
        .GetResultsInFullPath(source.BaseDirectory)
        .OrderBy(path => path, StringComparer.Ordinal);

    private static async Task<DdlDocument> ReadDocument(string path, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(path, cancellationToken);
        return DdlReader.Instance.Read(text);
    }
}
