
namespace NSchema.Project.Nsql;

/// <summary>
/// Thrown when NSchema source cannot be lexed or parsed.
/// </summary>
internal sealed class NsqlSyntaxException : Exception
{
    /// <summary>
    /// The position in the source where the error was detected.
    /// </summary>
    public SourcePosition Position { get; }

    /// <param name="message">A description of the problem (without the position; it is appended).</param>
    /// <param name="position">Where in the source the problem was detected.</param>
    public NsqlSyntaxException(string message, SourcePosition position)
        : base($"{message} (at {position}).")
    {
        Position = position;
    }
}
