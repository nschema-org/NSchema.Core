namespace NSchema.Plugins;

/// <summary>
/// The outcome of a plugin configuring itself — success, or failure with the reasons why.
/// </summary>
public sealed record PluginConfigureResult
{
    private PluginConfigureResult(bool succeeded, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Errors = errors;
    }

    /// <summary>
    /// Whether the plugin configured itself successfully.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// The reasons the plugin could not be configured; empty when <see cref="Succeeded"/> is <see langword="true"/>.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// A successful result.
    /// </summary>
    public static PluginConfigureResult Success { get; } = new(true, []);

    /// <summary>
    /// A failed result carrying the reasons the plugin could not be configured.
    /// </summary>
    /// <param name="errors">The configuration errors.</param>
    public static PluginConfigureResult Failure(params string[] errors) => new(false, errors);
}
