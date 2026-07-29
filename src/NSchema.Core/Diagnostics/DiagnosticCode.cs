using System.Text.RegularExpressions;

namespace NSchema.Diagnostics;

/// <summary>
/// What identifies a finding, independently of how it is worded.
/// </summary>
/// <remarks>
/// A code is a stable contract, not a description: it names a finding in configuration and documentation, so a
/// message can be reworded freely while the code stays put. It is restricted to what is safe as a settings key —
/// lowercase letters and digits in hyphen-separated words — and an invalid one is rejected rather than rewritten,
/// so a code that could not be configured never reaches a user.
/// </remarks>
public readonly partial record struct DiagnosticCode
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex Shape { get; }

    /// <summary>
    /// The code with the given value.
    /// </summary>
    /// <param name="value">The code, in hyphen-separated lowercase words.</param>
    /// <exception cref="ArgumentException">The value is not a usable code.</exception>
    public DiagnosticCode(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (!Shape.IsMatch(value))
        {
            throw new ArgumentException(
                $"'{value}' is not a diagnostic code. A code names a finding in hyphen-separated lowercase words " +
                "(e.g. 'missing-dialect'), so that it is usable as a settings key.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// The code as written.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;

    /// <summary>
    /// The code with the given value.
    /// </summary>
    /// <param name="value">The code, in hyphen-separated lowercase words.</param>
    public static implicit operator DiagnosticCode(string value) => new(value);
}
