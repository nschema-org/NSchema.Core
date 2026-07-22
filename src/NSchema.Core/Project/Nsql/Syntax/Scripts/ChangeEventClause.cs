using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql.Syntax.Scripts;

/// <summary>
/// <c>ON ADD COLUMN|ALTER COLUMN TYPE|ADD CONSTRAINT path</c>.
/// The path stays as written (unqualified inside a template body; the schema binds at projection).
/// </summary>
/// <param name="Trigger">The change trigger.</param>
/// <param name="Path">The target path as written.</param>
public sealed record ChangeEventClause(ChangeTrigger Trigger, MemberPath Path) : ScriptEventClause
{
    /// <summary>
    /// The keyword token(s) naming the change (e.g. <c>ADD COLUMN</c>), when parsed.
    /// </summary>
    public IReadOnlyList<Token> TriggerKeywords { get; init; } = [];

    internal override IEnumerable<NsqlChild> Children
    {
        get
        {
            foreach (var keyword in TriggerKeywords)
            {
                yield return keyword;
            }
            yield return Path;
        }
    }
}
