using System.Diagnostics.CodeAnalysis;
using NSchema.Model;

namespace NSchema.Configuration.Plugins;

/// <summary>
/// A package id: word-character segments joined by <c>.</c> or <c>-</c>. Equality is case-insensitive, as
/// package resolution's is.
/// </summary>
[System.ComponentModel.TypeConverter(typeof(PackageIdConverter))]
public sealed record PackageId : ValueObject<string>
{
    /// <summary>
    /// Wraps the id, throwing when it is not package-id-shaped. Check <see cref="IsValid"/> first to report
    /// bad input as a diagnostic instead.
    /// </summary>
    public PackageId(string value) : base(value)
    {
        if (!IsValid(value))
        {
            throw new ArgumentException($"'{value}' is not a valid package id.", nameof(value));
        }
    }

    /// <summary>
    /// Whether <paramref name="id"/> is package-id-shaped.
    /// </summary>
    public static bool IsValid(string id)
    {
        var separated = true;
        foreach (var character in id)
        {
            if (char.IsAsciiLetterOrDigit(character) || character == '_')
            {
                separated = false;
            }
            else if (character is '.' or '-' && !separated)
            {
                separated = true;
            }
            else
            {
                return false;
            }
        }
        return id.Length > 0 && !separated;
    }

    /// <summary>
    /// Case-insensitive equality over the written text.
    /// </summary>
    public bool Equals(PackageId? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// Wraps the id. One-way: an id never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator PackageId?(string? value) => value is null ? null : new(value);
}
