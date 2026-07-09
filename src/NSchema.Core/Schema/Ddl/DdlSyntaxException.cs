using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Thrown when NSchema DDL cannot be lexed or parsed.
/// </summary>
public sealed class DdlSyntaxException : Exception
{
    private readonly string _error;

    /// <summary>
    /// The position in the source where the error was detected.
    /// </summary>
    public SourcePosition Position { get; }

    /// <summary>
    /// The file (or other source name) the DDL came from, when known.
    /// </summary>
    public string? SourceName { get; }

    /// <param name="message">A description of the problem (without the position; it is appended).</param>
    /// <param name="position">Where in the source the problem was detected.</param>
    /// <param name="sourceName">The file the DDL came from, when known; included in the message.</param>
    public DdlSyntaxException(string message, SourcePosition position, string? sourceName = null)
        : base(sourceName is null ? $"{message} (at {position})." : $"{message} (at {position} in {sourceName}).")
    {
        _error = message;
        Position = position;
        SourceName = sourceName;
    }

    /// <summary>
    /// Returns a copy of this exception naming the source it was detected in.
    /// </summary>
    public DdlSyntaxException WithSource(string sourceName) => new(_error, Position, sourceName);
}
