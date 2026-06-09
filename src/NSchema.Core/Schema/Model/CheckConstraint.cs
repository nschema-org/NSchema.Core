using System.Diagnostics;

namespace NSchema.Schema.Model;

/// <summary>
/// Represents a check constraint in a database schema. The <see cref="Expression"/> is an opaque SQL predicate
/// passed through to the generator verbatim; it is the one place dialect-specific SQL can appear on a constraint.
/// </summary>
/// <param name="Name">The name of the check constraint.</param>
/// <param name="Expression">The SQL boolean expression the constraint enforces.</param>
[DebuggerDisplay("{Name,nq}: {Expression,nq}")]
public record CheckConstraint(string Name, string Expression);
