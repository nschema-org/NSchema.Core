using NSchema.Project.Nsql.Syntax.Config;

namespace NSchema.Plugins;

/// <summary>
/// The configuration a plugin is handed.
/// </summary>
/// <param name="Label">The optional bare-identifier label following the keyword (e.g. <c>"postgres"</c> in <c>PROVIDER postgres</c>).</param>
/// <param name="Attributes">The statement's attributes.</param>
public sealed record PluginSettings(string? Label, IReadOnlyDictionary<string, ConfigValue> Attributes)
{
    /// <summary>
    /// Returns the named attribute, or <see langword="null"/> if the settings do not declare it.
    /// </summary>
    public ConfigValue? Attribute(string name) => Attributes.GetValueOrDefault(name);

    /// <summary>
    /// Translates a parsed configuration statement into the settings payload.
    /// </summary>
    public static PluginSettings From(ConfigStatement statement)
    {
        var attributes = new Dictionary<string, ConfigValue>(StringComparer.OrdinalIgnoreCase);
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

        return new PluginSettings(statement.Label?.Value, attributes);
    }
}
