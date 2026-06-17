using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// Thrown when NSchema DDL cannot be lexed or parsed.
/// </summary>
public sealed class DdlSyntaxException : Exception
{
    /// <summary>
    /// The position in the source where the error was detected.
    /// </summary>
    public SourcePosition Position { get; }

    /// <param name="message">A description of the problem (without the position; it is appended).</param>
    /// <param name="position">Where in the source the problem was detected.</param>
    public DdlSyntaxException(string message, SourcePosition position)
        : base($"{message} (at {position}).")
    {
        Position = position;
    }
}
