using System.Diagnostics.CodeAnalysis;
using NSchema.Model;

namespace NSchema.Plugins.Model.Config;

/// <summary>
/// A configuration attribute key as written (<c>connection_string</c>, dotted <c>pool.max</c>).
/// Equality is case-insensitive, matching the grammar's duplicate-attribute rule.
/// </summary>
/// <remarks>
/// The key owns the binding convention: underscores and case are insignificant when a key names a .NET
/// member (<see cref="Matches"/>), and <see cref="ForProperty"/> renders the inverse.
/// </remarks>
public sealed record AttributeKey : ValueObject<string>
{
    /// <summary>
    /// Wraps the key as written.
    /// </summary>
    public AttributeKey(string value) : base(value)
    {
    }

    /// <summary>
    /// Wraps the key as written. One-way: a key never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator AttributeKey?(string? value) => value is null ? null : new(value);

    /// <summary>
    /// Case-insensitive equality over the written text.
    /// </summary>
    public bool Equals(AttributeKey? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// The dotted segments, outermost first; an undotted key is its own single segment.
    /// </summary>
    public IReadOnlyList<AttributeKey> Segments => Value.Split('.').Select(static s => new AttributeKey(s)).ToList();

    /// <summary>
    /// Whether the key names <paramref name="name"/> under the binding convention: underscores and case
    /// are insignificant (<c>connection_string</c> names <c>ConnectionString</c>).
    /// </summary>
    public bool Matches(string name) =>
        string.Equals(Value.Replace("_", ""), name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The key that binds onto <paramref name="propertyName"/>: <c>ConnectionString</c> → <c>connection_string</c>.
    /// </summary>
    public static AttributeKey ForProperty(string propertyName) =>
        new(string.Concat(propertyName.Select(static (c, i) => char.IsUpper(c) && i > 0 ? $"_{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}")));
}
