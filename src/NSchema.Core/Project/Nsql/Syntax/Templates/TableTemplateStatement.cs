using NSchema.Project.Nsql.Syntax;
using NSchema.Project.Nsql.Syntax.Tables;

namespace NSchema.Project.Nsql.Syntax.Templates;

/// <summary>
/// <c>TEMPLATE name FOR TABLE BEGIN members… END;</c> — a reusable member group merged into a table via
/// an <c>INCLUDE</c> member.
/// </summary>
/// <param name="Name">The template name.</param>
/// <param name="Members">The body members, unexpanded.</param>
public sealed record TableTemplateStatement(Identifier Name, SeparatedSyntaxList<TableMember> Members) : TemplateStatement(Name)
{
    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            if (DocComment is { } doc)
            {
                yield return doc;
            }
            if (TemplateKeyword is { } template)
            {
                yield return template;
            }
            yield return Name;
            if (ForKeyword is { } forKeyword)
            {
                yield return forKeyword;
            }
            if (KindKeyword is { } kind)
            {
                yield return kind;
            }
            if (BeginKeyword is { } begin)
            {
                yield return begin;
            }
            foreach (var child in Members.Children)
            {
                yield return child;
            }
            if (EndKeyword is { } end)
            {
                yield return end;
            }
            if (SemicolonToken is { } semicolon)
            {
                yield return semicolon;
            }
        }
    }
}
