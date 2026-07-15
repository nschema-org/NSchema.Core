namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// A <c>TEMPLATE name … BEGIN … END;</c> declaration.
/// </summary>
/// <param name="Name">The template name.</param>
public abstract record TemplateStatement(Identifier Name) : NsqlStatement;
