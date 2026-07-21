using NSchema.Project.Nsql.Syntax.Blocks;
using NSchema.Project.Nsql.Tokens;

namespace NSchema.Project.Nsql;

internal sealed partial class NsqlParser
{
    /// <summary>
    /// Parses the whole document as a configuration file: a sequence of blocks, of which only PLUGIN, ENGINE,
    /// DATABASE, and STATE are legal. Configuration and project statements never share a file.
    /// </summary>
    public NsqlBlockDocument ParseConfiguration() => new(ParseDocumentBody(ParseConfigurationBlock));

    private BlockStatement ParseConfigurationBlock(string? doc)
    {
        if (CurrentBlockKeyword() is not { } keyword || keyword == BlockKeyword.Lock)
        {
            throw _current.Kind == TokenKind.Identifier
                ? Error($"Unknown configuration statement '{_current.Text}'; a configuration file holds only PLUGIN, ENGINE, DATABASE, and STATE statements.")
                : Error($"Unexpected '{_current.Text}'; expected a configuration statement.");
        }
        return ParseBlock(keyword, doc);
    }
}
