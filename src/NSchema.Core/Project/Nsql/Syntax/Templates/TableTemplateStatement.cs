using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>TEMPLATE name FOR TABLE BEGIN members… END;</c> — a reusable member group merged into a table via
/// an <c>INCLUDE</c> member.
/// </summary>
/// <param name="Name">The template name.</param>
/// <param name="Members">The body members, unexpanded.</param>
public sealed record TableTemplateStatement(Identifier Name, IReadOnlyList<TableMember> Members) : NsqlStatement;
