namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>PLUGIN &lt;label&gt; ( source = '…', version = '…' );</c> — declares a plugin dependency: the package
/// that supplies it and the version (or range) to resolve. The label is the local name <c>DATABASE</c> and
/// <c>STATE</c> statements reference the plugin by, and the grammar requires it.
/// </summary>
/// <param name="Label">The local name the plugin is referenced by.</param>
/// <param name="Attributes">The attribute list.</param>
public sealed record PluginStatement(Identifier? Label, IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(Label, Attributes);
