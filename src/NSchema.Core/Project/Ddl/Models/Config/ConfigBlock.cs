namespace NSchema.Project.Ddl.Models.Config;

/// <summary>
/// A top-level configuration block declared in the DDL, e.g. <c>PROVIDER postgres ( … );</c>.
/// </summary>
/// <param name="Type">The block keyword, lower-cased for stable matching (<c>"backend"</c> or <c>"provider"</c>).</param>
/// <param name="Label">The optional bare-identifier label following the keyword (e.g. <c>"postgres"</c> in <c>PROVIDER postgres</c>, <c>"file"</c> in <c>BACKEND file</c>). </param>
/// <param name="Attributes">The block's <c>key = value</c> attributes. Keys are compared case-insensitively; dotted keys (e.g. <c>pool.max</c>) are preserved verbatim as a single key.</param>
public sealed record ConfigBlock(
    string Type,
    string? Label,
    IReadOnlyDictionary<string, ConfigValue> Attributes
)
{
    /// <summary>
    /// Returns the named attribute, or <see langword="null"/> if the block does not declare it.
    /// </summary>
    public ConfigValue? Attribute(string name) => Attributes.GetValueOrDefault(name);
}
