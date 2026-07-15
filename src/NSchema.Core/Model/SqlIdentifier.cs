namespace NSchema.Model;

/// <summary>
/// A SQL object name.
/// </summary>
public sealed record SqlIdentifier : ValueObject<string>, IComparable<SqlIdentifier>
{
    /// <summary>
    /// Wraps the name as written.
    /// </summary>
    public SqlIdentifier(string value) : base(value)
    {
    }

    /// <summary>
    /// Case-insensitive equality.
    /// </summary>
    public bool Equals(SqlIdentifier? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc />
    public int CompareTo(SqlIdentifier? other) => StringComparer.OrdinalIgnoreCase.Compare(Value, other?.Value);
}
