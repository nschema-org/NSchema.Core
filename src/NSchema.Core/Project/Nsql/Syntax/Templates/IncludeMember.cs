using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// An <c>INCLUDE template</c> member: merges a table template's members at this position.
/// </summary>
/// <param name="TemplateName">The included table template's name.</param>
public sealed record IncludeMember(Identifier TemplateName) : TableMember;
