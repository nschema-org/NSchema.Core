using System.Diagnostics;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;

namespace NSchema.Model.Domains;

/// <summary>
/// Represents a database domain: a schema-scoped named type built on a base type.
/// </summary>
[DebuggerDisplay("{Name,nq} (domain)")]
public sealed class DomainType : DatabaseObject, IEquatable<DomainType>
{
    /// <summary>
    /// Creates a domain, adopting its checks.
    /// </summary>
    /// <param name="name">The name of the domain.</param>
    /// <param name="dataType">The underlying base type (e.g. <c>text</c>). It cannot be altered in place, so a change to it is planned as a drop + recreate.</param>
    /// <param name="default">An optional default expression, stored verbatim (opaque SQL); <see langword="null"/> when none.</param>
    /// <param name="notNull">Whether the domain forbids <c>NULL</c>.</param>
    /// <param name="checks">The domain's <c>CHECK</c> constraints (their expressions reference the domain's <c>VALUE</c>); empty when none.</param>
    public DomainType(
        SqlIdentifier name,
        SqlType dataType,
        SqlText? @default = null,
        bool notNull = false,
        IReadOnlyList<CheckConstraint>? checks = null
    ) : base(name)
    {
        DataType = dataType;
        Default = @default;
        NotNull = notNull;
        Checks = checks ?? [];
    }

    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Domain;

    /// <summary>
    /// The underlying base type; a change to it is planned as a drop + recreate.
    /// </summary>
    public SqlType DataType { get; init; }

    /// <summary>
    /// An optional default expression, stored verbatim (opaque SQL); <see langword="null"/> when none.
    /// </summary>
    public SqlText? Default { get; init; }

    /// <summary>
    /// Whether the domain forbids <c>NULL</c>.
    /// </summary>
    public bool NotNull { get; init; }

    /// <summary>
    /// The domain's <c>CHECK</c> constraints; empty when none.
    /// </summary>
    public IReadOnlyList<CheckConstraint> Checks { get; init => field = [..value.Select(c => { c.Parent = this; return c; })]; }

    /// <summary>
    /// Returns a copy of the domain built on the given base type, outside any tree.
    /// </summary>
    public DomainType WithDataType(SqlType dataType) => new(Name, dataType, Default, NotNull, [.. Checks.Select(c => c.Clone())]) {Comment = Comment};

    internal DomainType Clone() => new(Name, DataType, Default, NotNull, [.. Checks.Select(c => c.Clone())]) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the schema and the comment are excluded.
    /// </summary>
    public bool Equals(DomainType? other) =>
        other is not null
        && Name == other.Name
        && DataType == other.DataType
        && Equals(Default, other.Default)
        && NotNull == other.NotNull
        && Checks.SequenceEqual(other.Checks);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DomainType other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, DataType, Default, NotNull);
}
