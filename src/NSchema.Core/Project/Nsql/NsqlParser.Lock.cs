using NSchema.Project.Nsql.Syntax.Config;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses the whole document under the lockfile grammar: only <c>LOCK</c> statements are legal. The lockfile
    /// is a machine-managed artifact, distinct from the configuration and project grammars.
    /// </summary>
    public NsqlLockDocument ParseLock() => new(ParseDocumentBody(ParseLockGrammarStatement));

    private ConfigStatement ParseLockGrammarStatement(string? doc)
    {
        if (CurrentConfigKeyword() != ConfigKeyword.Lock)
        {
            throw _current.Kind == TokenKind.Identifier
                ? Error($"Unknown lockfile statement '{_current.Text}'; a lockfile holds only LOCK statements.")
                : Error($"Unexpected '{_current.Text}'; expected a LOCK statement.");
        }

        return ParseKeywordStatement(ConfigKeyword.Lock, doc);
    }
}
