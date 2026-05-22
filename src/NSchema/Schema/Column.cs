using System.Diagnostics;

namespace NSchema.Schema;

[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record Column(
    string Name,
    SqlType Type,
    bool IsNullable = true,
    bool IsIdentity = false,
    string? DefaultExpression = null,
    string? PreviousName = null,
    string? Comment = null,
    IdentityOptions? IdentityOptions = null
)
{
    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression is { } d ? $" DEFAULT {d}" : "");
}
