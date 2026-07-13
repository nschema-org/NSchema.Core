namespace NSchema.Project.Domain.Models;

/// <summary>
/// A SQL object name.
/// </summary>
public readonly struct SqlIdentifier(string value) : IEquatable<SqlIdentifier>, IComparable<SqlIdentifier>
{
    /// <summary>
    /// The identifier text, in the casing it was written with.
    /// </summary>
    public string Value { get; } = value;

    /// <inheritdoc />
    public bool Equals(SqlIdentifier other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SqlIdentifier other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc />
    public int CompareTo(SqlIdentifier other) => StringComparer.OrdinalIgnoreCase.Compare(Value, other.Value);

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// Case-insensitive equality.
    /// </summary>
    public static bool operator ==(SqlIdentifier left, SqlIdentifier right) => left.Equals(right);

    /// <summary>
    /// Case-insensitive inequality.
    /// </summary>
    public static bool operator !=(SqlIdentifier left, SqlIdentifier right) => !left.Equals(right);
}
