using NSchema.Operations;

namespace NSchema;

/// <summary>
/// Options for configuring the behavior of an <see cref="NSchemaApplication"/>.
/// </summary>
public class NSchemaApplicationOptions
{
    /// <summary>
    /// Gets or sets the command-line arguments to add to the <see cref="NSchemaApplicationBuilder.Configuration"/>.
    /// </summary>
    public string[]? Args { get; init; }

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    public string? EnvironmentName { get; init; }

    /// <summary>
    /// Gets or sets the application name.
    /// </summary>
    public string? ApplicationName { get; init; }

    /// <summary>
    /// Gets or sets the content root path.
    /// </summary>
    public string? ContentRootPath { get; init; }

    /// <summary>
    /// Controls how exceptions are surfaced.
    /// </summary>
    public ExceptionBehavior ExceptionBehavior { get; init; } = ExceptionBehavior.ReportAndThrow;

    /// <summary>
    /// The output reporter key, resolved to an <see cref="IOperationReporter"/> at runtime.
    /// Defaults to the human-readable terminal reporter.
    /// </summary>
    public string Reporter { get; init; } = DefaultOperationReporter.ReporterName;
}
