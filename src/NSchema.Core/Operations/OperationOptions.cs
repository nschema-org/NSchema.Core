using NSchema.Hosting;

namespace NSchema.Operations;

/// <summary>
/// Configures how an operation is executed.
/// </summary>
public class OperationOptions
{
    /// <summary>
    /// The default output reporter: human-readable terminal output.
    /// </summary>
    public const string DefaultReporter = DefaultOperationReporter.ReporterName;

    /// <summary>
    /// The format used to render output, resolved to an <see cref="IOperationReporter"/> at runtime.
    /// </summary>
    public string Reporter { get; set; } = DefaultReporter;
}
