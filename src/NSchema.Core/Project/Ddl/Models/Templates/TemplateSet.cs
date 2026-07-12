namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// The template constructs parsed from DDL: definitions, applications, and includes.
/// </summary>
/// <param name="Definitions">The <c>TEMPLATE … BEGIN … END;</c> definitions.</param>
/// <param name="Applications">The <c>APPLY TEMPLATE … IN SCHEMA …;</c> statements.</param>
/// <param name="Includes">The <c>INCLUDE</c> members written in table bodies, targeting their tables by name.</param>
public sealed record TemplateSet(
    IReadOnlyList<TemplateDefinition>? Definitions = null,
    IReadOnlyList<TemplateApplication>? Applications = null,
    IReadOnlyList<TemplateInclude>? Includes = null
)
{
    /// <summary>
    /// The <c>TEMPLATE … BEGIN … END;</c> definitions. Definitions are inert until applied or included.
    /// </summary>
    public IReadOnlyList<TemplateDefinition> Definitions { get; init; } = Definitions ?? [];

    /// <summary>
    /// The <c>APPLY TEMPLATE … IN SCHEMA …;</c> statements.
    /// </summary>
    public IReadOnlyList<TemplateApplication> Applications { get; init; } = Applications ?? [];

    /// <summary>
    /// The <c>INCLUDE</c> members written in table bodies, targeting their tables by name.
    /// </summary>
    public IReadOnlyList<TemplateInclude> Includes { get; init; } = Includes ?? [];

    /// <summary>
    /// Combines the current <see cref="TemplateSet"/> with another.
    /// </summary>
    /// <param name="templates">The set to combine with.</param>
    public TemplateSet Combine(TemplateSet templates) => new(
        [.. Definitions, .. templates.Definitions],
        [.. Applications, .. templates.Applications],
        [.. Includes, .. templates.Includes]);
}
