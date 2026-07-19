namespace NSchema.Plugins;

/// <summary>
/// A scalar value of a configuration attribute (see <see cref="PluginConfig"/>).
/// </summary>
public sealed record ConfigValue
{
    private ConfigValue(ConfigValueKind kind, object value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary
    /// >The kind of scalar held.
    /// </summary>
    public ConfigValueKind Kind { get; }

    /// <summary>
    /// The boxed underlying value (<see cref="string"/>, <see cref="long"/>, or <see cref="bool"/>).
    /// </summary>
    public object Value { get; }

    /// <summary>
    /// Creates a <see cref="ConfigValueKind.String"/> value.
    /// </summary>
    public static ConfigValue OfString(string value) => new(ConfigValueKind.String, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>
    /// Creates an <see cref="ConfigValueKind.Integer"/> value.
    /// </summary>
    public static ConfigValue OfInteger(long value) => new(ConfigValueKind.Integer, value);

    /// <summary>
    /// Creates a <see cref="ConfigValueKind.Boolean"/> value.
    /// </summary>
    public static ConfigValue OfBoolean(bool value) => new(ConfigValueKind.Boolean, value);

    /// <summary>
    /// Creates an <see cref="ConfigValueKind.Identifier"/> value.
    /// </summary>
    public static ConfigValue OfIdentifier(string value) => new(ConfigValueKind.Identifier, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>
    /// The text of a <see cref="ConfigValueKind.String"/> or <see cref="ConfigValueKind.Identifier"/> value.
    /// </summary>
    public string AsString() => Kind is ConfigValueKind.String or ConfigValueKind.Identifier
        ? (string)Value
        : throw new InvalidOperationException($"Configuration value of kind {Kind} is not a string.");

    /// <summary>
    /// The value of an <see cref="ConfigValueKind.Integer"/> value.
    /// </summary>
    public long AsInteger() => Kind is ConfigValueKind.Integer
        ? (long)Value
        : throw new InvalidOperationException($"Configuration value of kind {Kind} is not an integer.");

    /// <summary>
    /// The value of a <see cref="ConfigValueKind.Boolean"/> value.
    /// </summary>
    public bool AsBoolean() => Kind is ConfigValueKind.Boolean
        ? (bool)Value
        : throw new InvalidOperationException($"Configuration value of kind {Kind} is not a boolean.");

    /// <inheritdoc/>
    public override string ToString() => Kind switch
    {
        ConfigValueKind.String => $"'{Value}'",
        ConfigValueKind.Boolean => (bool)Value ? "true" : "false",
        _ => Value.ToString()!,
    };
}
