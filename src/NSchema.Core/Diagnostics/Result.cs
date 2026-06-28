namespace NSchema.Diagnostics;

/// <summary>
/// The outcome of an operation that either succeeds or fails with reasons, but yields no value.
/// </summary>
public class Result
{
    private protected Result(IReadOnlyList<Diagnostic> diagnostics) => Diagnostics = diagnostics;

    /// <summary>
    /// Every finding produced, of any severity. Empty on a clean success.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Whether the operation succeeded; true if there are no errors.
    /// </summary>
    public virtual bool IsSuccess => !Errors.Any();

    /// <summary>
    /// Whether the operation failed; true if there are errors.
    /// </summary>
    public virtual bool IsFailure => !IsSuccess;

    /// <summary>
    /// A result the caller expects to be successful, optionally carrying advisory (info/warning) diagnostics.
    /// </summary>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    public static Result Success(params IEnumerable<Diagnostic> diagnostics) => new([.. diagnostics]);

    /// <summary>
    /// A result the caller knows is a failure, carrying the error diagnostics that explain it.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    public static Result Failure(params IEnumerable<Diagnostic> diagnostics) => new([.. diagnostics]);

    /// <summary>
    /// A result built from an aggregated set of diagnostics.
    /// </summary>
    /// <param name="diagnostics">Every finding produced.</param>
    public static Result From(IEnumerable<Diagnostic> diagnostics) => new([.. diagnostics]);
}
