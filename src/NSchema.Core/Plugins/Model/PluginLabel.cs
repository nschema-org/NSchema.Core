using System.Diagnostics.CodeAnalysis;
using NSchema.Model;

namespace NSchema.Plugins.Model;

/// <summary>
/// The local name a configuration refers to a plugin by: the label on a <c>PLUGIN</c> statement, referenced
/// by <c>DATABASE</c> and <c>STATE</c>.
/// </summary>
public sealed record PluginLabel : ValueObject<string>
{
    /// <summary>
    /// Wraps the label as written.
    /// </summary>
    public PluginLabel(string value) : base(value)
    {
    }

    /// <summary>
    /// Case-insensitive equality over the written text.
    /// </summary>
    public bool Equals(PluginLabel? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <summary>
    /// Wraps the label as written. One-way: a label never converts silently back to a bare string.
    /// </summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static implicit operator PluginLabel?(string? value) => value is null ? null : new(value);
}
