namespace NSchema.Diagnostics;

/// <summary>
/// The outcome of an operation that either succeeds or fails with reasons, but yields no value.
/// </summary>
public sealed class Result
{
    private Result(bool isSuccess, IReadOnlyList<Diagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the operation failed; the inverse of <see cref="IsSuccess"/>.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Every finding produced, of any severity. Empty on a clean success.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/> — the reasons a failure failed.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// A successful result, optionally carrying advisory (info/warning) diagnostics.
    /// </summary>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    /// <returns>A successful <see cref="Result"/>.</returns>
    public static Result Success(params Diagnostic[] diagnostics) => new(true, diagnostics);

    /// <summary>
    /// A failed result carrying the diagnostics that explain the failure.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(params Diagnostic[] diagnostics) => new(false, diagnostics);

    /// <summary>
    /// A failed result carrying the diagnostics that explain the failure.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    /// <returns>A failed <see cref="Result"/>.</returns>
    public static Result Failure(IEnumerable<Diagnostic> diagnostics) => new(false, diagnostics.ToArray());
}
