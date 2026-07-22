using NSchema.Project.Nsql.Syntax.Blocks;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses the whole document under the lockfile grammar: only <c>LOCK</c> statements are legal. The lockfile
    /// is a machine-managed artifact, distinct from the configuration and project grammars.
    /// </summary>
    public NsqlBlockDocument ParseLock()
    {
        var statements = ParseDocumentBody(ParseLockBlock);
        return new NsqlBlockDocument(statements) { EndOfFile = _current };
    }

    private BlockStatement ParseLockBlock(Token? doc)
    {
        if (CurrentBlockKeyword() != BlockKeyword.Lock)
        {
            throw _current.Kind == TokenKind.Identifier
                ? Error($"Unknown lockfile statement '{_current.Text}'; a lockfile holds only LOCK statements.")
                : Error($"Unexpected '{_current.Text}'; expected a LOCK statement.");
        }

        return ParseBlock(BlockKeyword.Lock, doc);
    }
}
