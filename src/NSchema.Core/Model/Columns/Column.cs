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
    public SqlType Type { get; init; } = type;

    /// <summary>
    /// A boolean value indicating whether the column allows NULL values.
    /// </summary>
    public bool IsNullable { get; init; } = isNullable;

    /// <summary>
    /// A boolean value indicating whether the column is an identity column.
    /// </summary>
    public bool IsIdentity { get; init; } = isIdentity;

    /// <summary>
    /// An optional default expression for the column.
    /// </summary>
    public SqlText? DefaultExpression { get; init; } = defaultExpression;

    /// <summary>
    /// An optional set of options for identity columns, such as seed and increment values.
    /// </summary>
    public IdentityOptions? IdentityOptions { get; init; } = identityOptions;

    /// <summary>
    /// An optional expression for a stored generated column; mutually exclusive with a default.
    /// </summary>
    public SqlText? GeneratedExpression { get; init; } = generatedExpression;

    /// <summary>
    /// Returns a copy of the column with the given type, outside any tree.
    /// </summary>
    public Column WithType(SqlType type) => Clone(type);

    /// <summary>
    /// Returns a nullable copy of the column, outside any tree — the shape a backfilled column add starts in.
    /// </summary>
    public Column AsNullable() =>
        new(Name, Type, isNullable: true, IsIdentity, DefaultExpression, IdentityOptions, GeneratedExpression)
        {
            Comment = Comment
        };

    internal Column Clone(SqlType? type = null) =>
        new(Name, type ?? Type, IsNullable, IsIdentity, DefaultExpression, IdentityOptions, GeneratedExpression)
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
