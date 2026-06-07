using NSchema.Operations;

namespace NSchema.Hosting;

/// <summary>
/// Controls how unhandled exceptions are surfaced.
/// </summary>
public enum ExceptionBehavior
{
    /// <summary>
    /// Present the exception via <see cref="IOperationReporter.ReportException(System.Exception)"/>, then rethrow it.
    /// </summary>
    ReportAndThrow,

    /// <summary>
    /// Rethrow the exception without presenting it.
    /// </summary>
    Throw,
}
