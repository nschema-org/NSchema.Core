using System.Diagnostics;

namespace NSchema.Model.Columns;

/// <summary>
/// Represents a column within a database table.
/// </summary>
/// <param name="name">The name of the column.</param>
/// <param name="type">The SQL data type of the column.</param>
/// <param name="isNullable">A boolean value indicating whether the column allows NULL values.</param>
/// <param name="isIdentity">A boolean value indicating whether the column is an identity column.</param>
/// <param name="defaultExpression">An optional default expression for the column.</param>
/// <param name="identityOptions">An optional set of options for identity columns, such as seed and increment values.</param>
/// <param name="generatedExpression">An optional expression for a stored generated column (<c>GENERATED ALWAYS AS (expr) STORED</c>); mutually exclusive with a default.</param>
[DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class Column(
    SqlIdentifier name,
    SqlType type,
    bool isNullable = false,
    bool isIdentity = false,
    SqlText? defaultExpression = null,
    IdentityOptions? identityOptions = null,
    SqlText? generatedExpression = null
) : DatabaseMember(name), IEquatable<Column>
{
    /// <summary>
    /// The SQL data type of the column.
    /// </summary>
    public SqlType Type { get; set; } = type;

    /// <summary>
    /// A boolean value indicating whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; set; } = isNullable;

    /// <summary>
    /// A boolean value indicating whether the column is an identity column.
    /// </summary>
    public bool IsIdentity { get; set; } = isIdentity;

    /// <summary>
    /// An optional default expression for the column.
    /// </summary>
    public SqlText? DefaultExpression { get; set; } = defaultExpression;

    /// <summary>
    /// An optional set of options for identity columns, such as seed and increment values.
    /// </summary>
    public IdentityOptions? IdentityOptions { get; set; } = identityOptions;

    /// <summary>
    /// An optional expression for a stored generated column; mutually exclusive with a default.
    /// </summary>
    public SqlText? GeneratedExpression { get; set; } = generatedExpression;

    /// <inheritdoc/>
    public override Column Clone() =>
        new(Name, Type, IsNullable, IsIdentity, DefaultExpression, IdentityOptions, GeneratedExpression)
        {
            Comment = Comment
        };

    /// <summary>
    /// Structural equality over the declared definition; the parent and the comment are excluded.
    /// </summary>
    public bool Equals(Column? other) =>
        other is not null
        && Name == other.Name
        && Type == other.Type
        && IsNullable == other.IsNullable
        && IsIdentity == other.IsIdentity
        && Equals(DefaultExpression, other.DefaultExpression)
        && Equals(IdentityOptions, other.IdentityOptions)
        && Equals(GeneratedExpression, other.GeneratedExpression);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Column other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Name, Type, IsNullable, IsIdentity, DefaultExpression, IdentityOptions, GeneratedExpression);

    private string DebuggerDisplay =>
        $"{Name} {Type}" +
        (IsNullable ? " NULL" : " NOT NULL") +
        (IsIdentity ? " IDENTITY" : "") +
        (DefaultExpression == null ? "" : $" DEFAULT {DefaultExpression}") +
        (GeneratedExpression == null ? "" : $" GENERATED AS {GeneratedExpression}");
}
