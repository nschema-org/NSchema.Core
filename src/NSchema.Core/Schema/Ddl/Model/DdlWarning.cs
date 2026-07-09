namespace NSchema.Schema.Ddl.Model;

/// <summary>
/// A non-fatal finding produced while parsing DDL (for example, use of deprecated syntax).
/// </summary>
/// <param name="Message">A description of the finding, without the position.</param>
/// <param name="Position">Where in the source the finding was detected.</param>
internal sealed record DdlWarning(string Message, SourcePosition Position);
