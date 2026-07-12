using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Columns;

/// <summary>
/// Represents a column within a database table.
/// </summary>
/// <param name="Name">The name of the column.</param>
/// <param name="Type">The SQL data type of the column.</param>
/// <param name="IsNullable">A boolean value indicating whether the column allows NULL values.</param>
/// <param name="IsIdentity">A boolean value indicating whether the column is an identity column.</param>
/// <param name="DefaultExpression">An optional default expression for the column.</param>
/// <param name="OldName">The previous name of the column, if it has been renamed.</param>
/// <param name="Comment">An optional comment or description for the column.</param>
/// <param name="IdentityOptions">An optional set of options for identity columns, such as seed and increment values.</param>
/// <param name="GeneratedExpression">An optional expression for a stored generated column (<c>GENERATED ALWAYS AS (expr) STORED</c>); mutually exclusive with a default.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public record Column(
    string Name,
    SqlType Type,
    bool IsNullable = false,
    bool IsIdentity = false,
    string? DefaultExpression = null,
    string? OldName = null,
    string? Comment = null,
    IdentityOptions? IdentityOptions = null,
    string? GeneratedExpression = null
) : IRenameableObject
{
    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression == null ? "" : $" DEFAULT {DefaultExpression}") +
        (GeneratedExpression == null ? "" : $" GENERATED AS {GeneratedExpression}");
}
