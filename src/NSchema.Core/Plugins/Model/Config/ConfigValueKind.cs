namespace NSchema.Plugins.Model.Config;

/// <summary>
/// The kind of scalar a <see cref="ConfigValue"/> holds.
/// </summary>
public enum ConfigValueKind
{
    /// <summary>
    /// A single-quoted string literal, e.g. <c>'postgres'</c>.
    /// </summary>
    String,

    /// <summary>
    /// An integer literal, e.g. <c>1000</c> (may be negative).
    /// </summary>
    Integer,

    /// <summary>
    /// A boolean literal, <c>true</c> or <c>false</c>.
    /// </summary>
    Boolean,

    /// <summary>
    /// A bare identifier used as a value, e.g. <c>single</c> in <c>transaction_mode = single</c>.
    /// </summary>
    Identifier
}
