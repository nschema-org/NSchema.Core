namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>PROVIDER [label] ( key = value, … );</c> — selects and configures the database provider.
/// </summary>
/// <param name="Label">The provider label (e.g. <c>postgres</c>).</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record ProviderStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(Label, Attributes);
