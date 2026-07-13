namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>APPLY TEMPLATE name IN SCHEMA a[, b]…;</c>
/// </summary>
/// <param name="TemplateName">The applied template's name.</param>
/// <param name="Schemas">The target schemas.</param>
public sealed record ApplyTemplateStatement(Identifier TemplateName, IReadOnlyList<Identifier> Schemas) : NsqlStatement;