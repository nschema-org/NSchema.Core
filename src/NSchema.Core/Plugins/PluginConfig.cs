namespace NSchema.Plugins;

/// <summary>
/// The configuration a plugin is handed.
/// </summary>
/// <param name="Label">The optional bare-identifier label following the keyword (e.g. <c>"postgres"</c> in <c>DATABASE postgres</c>).</param>
/// <param name="Attributes">The statement's attributes, keyed case-insensitively.</param>
public sealed record PluginConfig(string? Label, IReadOnlyDictionary<string, ConfigValue> Attributes)
{
    /// <summary>
    /// Returns the named attribute, or <see langword="null"/> if the settings do not declare it.
    /// </summary>
    public ConfigValue? Attribute(string name) => Attributes.GetValueOrDefault(name);

    /// <summary>
    /// Binds the attributes onto a new <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The options type. Must have a parameterless constructor..</typeparam>
    /// <remarks>
    /// snake_case attribute names map to property names (<c>connection_string</c> → <c>ConnectionString</c>),
    /// dotted keys to nested objects (<c>pool.max</c> → <c>Pool.Max</c>), and identifier values to enums.
    /// A <c>required</c> member with no matching attribute, an attribute matching no property, and a value that doesn't fit are all error diagnostics;
    /// the result still carries the best-effort instance.
    /// </remarks>
    public Result<T> Bind<T>() => PluginConfigBinder.Bind<T>(this);
}
