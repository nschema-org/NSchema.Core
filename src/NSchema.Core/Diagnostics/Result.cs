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

    /// <summary>
    /// A successful <see cref="Result{T}"/> carrying <paramref name="value"/>, optionally with advisory diagnostics.
    /// </summary>
    /// <typeparam name="T">The value produced on success.</typeparam>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    public static Result<T> Success<T>(T value, params IEnumerable<Diagnostic> diagnostics) => new(value, [.. diagnostics]);

    /// <summary>
    /// A failed <see cref="Result{T}"/> — no value — carrying the error diagnostics that explain it.
    /// </summary>
    /// <typeparam name="T">The value the result would have produced on success.</typeparam>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    public static Result<T> Failure<T>(params IEnumerable<Diagnostic> diagnostics) => new(default, [.. diagnostics]);

    /// <summary>
    /// A <see cref="Result{T}"/> built from <paramref name="value"/> plus an aggregated set of diagnostics, carrying the
    /// value whether or not the result is a failure.
    /// </summary>
    /// <typeparam name="T">The value produced.</typeparam>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Every finding produced.</param>
    public static Result<T> From<T>(T value, IEnumerable<Diagnostic> diagnostics) => new(value, [.. diagnostics]);
}
