using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a database function. Functions and procedures share one name space within a schema (as they do in
/// the database), and overloading is not supported: one function per name.
/// </summary>
/// <param name="Name">The name of the function.</param>
/// <param name="Arguments">The argument list, stored verbatim (the text inside the parentheses; may be empty).</param>
/// <param name="Definition">Everything after the argument list, stored verbatim (e.g. <c>RETURNS … LANGUAGE … AS
/// $$…$$</c>). Must not contain a top-level <c>;</c> outside a dollar-quoted string, or it will not survive a
/// write → parse round trip (the parser stops at the first top-level terminator).</param>
/// <param name="OldName">The previous name of the function, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the function.</param>
[DebuggerDisplay("{Name,nq} (function)")]
public sealed record Function(
    string Name,
    string Arguments,
    string Definition,
    string? OldName = null,
    string? Comment = null
);
