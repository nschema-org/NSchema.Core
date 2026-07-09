using NSchema.Schema.Ddl.Model;

namespace NSchema.Schema.Ddl;

/// <summary>
/// The outcome of a parse operation.
/// </summary>
/// <param name="Document">The parsed document.</param>
/// <param name="Warnings">Non-fatal findings raised while parsing (for example, use of deprecated syntax).</param>
internal sealed record DdlParseResult(DdlDocument Document, IReadOnlyList<DdlWarning> Warnings);
