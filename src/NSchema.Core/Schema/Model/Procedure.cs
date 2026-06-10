using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a database procedure. Functions and procedures share one name space within a schema (as they do in
/// the database), and overloading is not supported: one procedure per name.
/// </summary>
/// <param name="Name">The name of the procedure.</param>
/// <param name="Arguments">The argument list, stored verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="Definition">Everything after the argument list, stored verbatim (e.g. <c>LANGUAGE … AS $$…$$</c>).
/// Must not contain a top-level <c>;</c> outside a dollar-quoted string, or it will not survive a write → parse
/// round trip (the parser stops at the first top-level terminator).</param>
/// <param name="OldName">The previous name of the procedure, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the procedure.</param>
[DebuggerDisplay("{Name,nq} (procedure)")]
public sealed record Procedure(
    string Name,
    string Arguments,
    string Definition,
    string? OldName = null,
    string? Comment = null
);
