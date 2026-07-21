using NSchema.Project.Nsql.Syntax.Lock;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses the whole document under the lockfile grammar: only <c>LOCK</c> statements are legal. The lockfile
    /// is a machine-managed artifact, distinct from the configuration and project grammars.
    /// </summary>
    public NsqlLockDocument ParseLock() => new(ParseDocumentBody(ParseLockGrammarStatement));

    private LockStatement ParseLockGrammarStatement(string? doc)
    {
        if (!_current.IsKeyword(NsqlKeywords.Lock))
        {
            throw _current.Kind == TokenKind.Identifier
                ? Error($"Unknown lockfile statement '{_current.Text}'; a lockfile holds only LOCK statements.")
                : Error($"Unexpected '{_current.Text}'; expected a LOCK statement.");
        }

        var (position, label, attributes) = ParseConfigStatementBody();
        if (label is not null)
        {
            throw new NsqlSyntaxException("A LOCK statement takes no label; it identifies its package by the 'source' attribute.", label.Position);
        }
        return new LockStatement(attributes) { Position = position, Doc = doc };
    }
}
