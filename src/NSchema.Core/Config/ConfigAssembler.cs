using NSchema.Plugins;
using NSchema.Project.Nsql;
using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Config;

/// <summary>
/// Assembles parsed configuration documents into a <see cref="ConfigDefinition"/>, validating as it goes..
/// </summary>
public static class ConfigAssembler
{
    private const string SourceAttribute = "source";
    private const string VersionAttribute = "version";

    // The labels Core itself serves (the built-in file state store), which no PLUGIN statement declares.
    private static readonly HashSet<PluginLabel> _builtInLabels = [new("file")];

    /// <summary>
    /// Validates and resolves <paramref name="documents"/> into the configuration they declare.
    /// </summary>
    /// <param name="documents">The configuration documents resolved together, as read by <see cref="NsqlReader.ReadConfig"/>.</param>
    public static Result<ConfigDefinition, NsqlDiagnostic> Assemble(IReadOnlyList<NsqlConfigDocument> documents)
    {
        var diagnostics = new List<NsqlDiagnostic>();
        var plugins = new List<PluginDeclaration>();
        EngineConfig? engine = null;
        var references = new List<(NsqlConfigDocument Document, ConfigStatement Statement)>();

        // The declarations first: a reference may precede the PLUGIN that satisfies it.
        foreach (var (document, statement) in Statements(documents))
        {
            switch (statement)
            {
                case PluginStatement plugin:
                    if (Collect(ValidatePlugin(plugin), document, diagnostics))
                    {
                        break;
                    }
                    var declaration = new PluginDeclaration(plugin.Label!.Value, RequiredString(plugin, SourceAttribute), ParsedVersion(plugin));
                    if (plugins.Any(p => p.Label == declaration.Label))
                    {
                        diagnostics.Add(ConfigDiagnostics.DuplicatePluginLabel(declaration.Label, statement.Position) with { File = document.FilePath });
                    }
                    else if (plugins.Any(p => p.Source == declaration.Source))
                    {
                        diagnostics.Add(ConfigDiagnostics.DuplicatePluginSource(declaration.Source, statement.Position) with { File = document.FilePath });
                    }
                    else
                    {
                        plugins.Add(declaration);
                    }
                    break;
                case EngineStatement assertion:
                    if (Collect(ValidateClosedAttributes(assertion, "ENGINE", "'version'", VersionAttribute), document, diagnostics))
                    {
                        break;
                    }
                    if (engine is not null)
                    {
                        diagnostics.Add(ConfigDiagnostics.DuplicateStatement("ENGINE", statement.Position) with { File = document.FilePath });
                        break;
                    }
                    engine = new EngineConfig(new EngineRequirement(ParsedVersion(assertion)));
                    break;
                default:
                    references.Add((document, statement));
                    break;
            }
        }

        PluginConfig? database = null;
        PluginConfig? state = null;
        foreach (var (document, statement) in references)
        {
            var keyword = statement is DatabaseStatement ? "DATABASE" : "STATE";
            if (statement.Label is not { } label)
            {
                diagnostics.Add(ConfigDiagnostics.UnlabelledReference(keyword, statement.Position) with { File = document.FilePath });
                continue;
            }
            PluginLabel reference = label.Value;
            if (!plugins.Any(p => p.Label == reference) && !_builtInLabels.Contains(reference))
            {
                diagnostics.Add(ConfigDiagnostics.UnknownPluginLabel(keyword, label.Value, label.Position) with { File = document.FilePath });
                continue;
            }
            if ((statement is DatabaseStatement ? database : state) is not null)
            {
                diagnostics.Add(ConfigDiagnostics.DuplicateStatement(keyword, statement.Position) with { File = document.FilePath });
                continue;
            }
            var settings = Translate(statement);
            if (statement is DatabaseStatement)
            {
                database = settings;
            }
            else
            {
                state = settings;
            }
        }

        return Result<ConfigDefinition, NsqlDiagnostic>.From(new ConfigDefinition(plugins, engine, database, state), diagnostics);
    }

    /// <summary>
    /// Stamps the document's file onto <paramref name="findings"/> and collects them, reporting whether there
    /// were any — a statement with findings is excluded from the definition.
    /// </summary>
    private static bool Collect(IEnumerable<NsqlDiagnostic> findings, NsqlConfigDocument document, List<NsqlDiagnostic> diagnostics)
    {
        var count = diagnostics.Count;
        diagnostics.AddRange(findings.Select(d => d with { File = document.FilePath }));
        return diagnostics.Count > count;
    }

    private static IEnumerable<NsqlDiagnostic> ValidatePlugin(PluginStatement plugin)
    {
        foreach (var diagnostic in ValidateClosedAttributes(plugin, "PLUGIN", "'source' and 'version'", SourceAttribute, VersionAttribute))
        {
            yield return diagnostic;
        }
        if (Attribute(plugin, SourceAttribute) is { Value: StringValue source } attribute
            && !PackageId.IsValid(source.Value))
        {
            yield return ConfigDiagnostics.InvalidPackageId(source.Value, attribute.Position);
        }
    }

    private static IEnumerable<NsqlDiagnostic> ValidateClosedAttributes(ConfigStatement statement, string keyword, string known, params string[] names)
    {
        foreach (var name in names)
        {
            switch (Attribute(statement, name))
            {
                case null:
                    yield return ConfigDiagnostics.RequiredAttribute(keyword, name, statement.Position);
                    break;
                case { Value: not StringValue } attribute:
                    yield return ConfigDiagnostics.AttributeMustBeString(keyword, name, attribute.Position);
                    break;
            }
        }
        foreach (var attribute in statement.Attributes.Where(a => !names.Contains(a.Key, StringComparer.OrdinalIgnoreCase)))
        {
            yield return ConfigDiagnostics.UnknownAttribute(keyword, attribute.Key, known, attribute.Position);
        }
        if (Attribute(statement, VersionAttribute) is { Value: StringValue version } versionAttribute
            && !VersionRange.TryParse(version.Value, out _))
        {
            yield return ConfigDiagnostics.InvalidVersionRange(version.Value, versionAttribute.Position);
        }
    }

    /// <summary>
    /// Translates a validated statement into the <see cref="PluginConfig"/> a plugin is handed. Translation
    /// lives here, not on the domain models — only the assembly layer touches the syntax tree.
    /// </summary>
    private static PluginConfig Translate(ConfigStatement statement)
    {
        var attributes = new Dictionary<AttributeKey, ConfigValue>();
        foreach (var attribute in statement.Attributes)
        {
            attributes.Add(attribute.Key, attribute.Value switch
            {
                StringValue v => ConfigValue.OfString(v.Value),
                IntegerValue v => ConfigValue.OfInteger(v.Value),
                BooleanValue v => ConfigValue.OfBoolean(v.Value),
                IdentifierValue v => ConfigValue.OfIdentifier(v.Value),
                _ => throw new InvalidOperationException($"Untranslatable config value '{attribute.Value.GetType().Name}'."),
            });
        }

        return new PluginConfig(statement.Label?.Value, attributes);
    }

    // The reads below assume the statement passed validation: the attribute is present, is a string, and the
    // version normalizes.
    private static string RequiredString(ConfigStatement statement, string name) => ((StringValue)Attribute(statement, name)!.Value).Value;

    private static VersionRange ParsedVersion(ConfigStatement statement)
    {
        VersionRange.TryParse(RequiredString(statement, VersionAttribute), out var version);
        return version!;
    }

    private static IEnumerable<(NsqlConfigDocument Document, ConfigStatement Statement)> Statements(IReadOnlyList<NsqlConfigDocument> documents) =>
        documents.SelectMany(d => d.Statements.Select(s => (d, s)));

    private static ConfigAttribute? Attribute(ConfigStatement statement, string name) =>
        statement.Attributes.FirstOrDefault(a => string.Equals(a.Key, name, StringComparison.OrdinalIgnoreCase));

}
