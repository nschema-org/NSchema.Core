using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// The outcome of an operation that yields a <typeparamref name="T"/> on success or fails with reasons.
/// </summary>
/// <typeparam name="T">The value produced on success.</typeparam>
public sealed class Result<T>
{
    private Result(bool isSuccess, T? value, IReadOnlyList<Diagnostic> diagnostics)
    {
        IsSuccess = isSuccess;
        Value = value;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// Whether the operation succeeded. When <see langword="true"/>, <see cref="Value"/> is non-null.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess { get; }

    /// <summary>
    /// Whether the operation failed; the inverse of <see cref="IsSuccess"/>.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// The produced value; non-null exactly when <see cref="IsSuccess"/> is <see langword="true"/>.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Every finding produced, of any severity. Empty on a clean success.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/> — the reasons a failure failed.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// A successful result carrying <paramref name="value"/>, optionally with advisory (info/warning) diagnostics.
    /// </summary>
    /// <param name="value">The produced value.</param>
    /// <param name="diagnostics">Advisory diagnostics to surface alongside the success.</param>
    /// <returns>A successful <see cref="Result{T}"/>.</returns>
    public static Result<T> Success(T value, params Diagnostic[] diagnostics) => new(true, value, diagnostics);

    /// <summary>
    /// A failed result carrying the diagnostics that explain the failure.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure(params Diagnostic[] diagnostics) => new(false, default, diagnostics);

    /// <summary>
    /// A failed result carrying the diagnostics that explain the failure.
    /// </summary>
    /// <param name="diagnostics">The diagnostics describing why the operation failed.</param>
    /// <returns>A failed <see cref="Result{T}"/>.</returns>
    public static Result<T> Failure(IEnumerable<Diagnostic> diagnostics) => new(false, default, diagnostics.ToArray());

    /// <summary>
    /// Collapses the result to a single value by invoking the matching branch.
    /// </summary>
    /// <typeparam name="TResult">The type both branches produce.</typeparam>
    /// <param name="onSuccess">Invoked with <see cref="Value"/> when the result is successful.</param>
    /// <param name="onFailure">Invoked with <see cref="Diagnostics"/> when the result is a failure.</param>
    /// <returns>The value produced by the invoked branch.</returns>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<IReadOnlyList<Diagnostic>, TResult> onFailure) =>
        IsSuccess ? onSuccess(Value) : onFailure(Diagnostics);

    /// <summary>
    /// Projects a successful value through <paramref name="map"/>, propagating the diagnostics; a failure passes
    /// through unchanged.
    /// </summary>
    /// <typeparam name="TOut">The mapped value type.</typeparam>
    /// <param name="map">The projection applied to <see cref="Value"/> on success.</param>
    /// <returns>The mapped result.</returns>
    public Result<TOut> Map<TOut>(Func<T, TOut> map) =>
        IsSuccess
            ? Result<TOut>.Success(map(Value), Diagnostics.ToArray())
            : Result<TOut>.Failure(Diagnostics);

    /// <summary>
    /// Lifts a value into a successful result, so a method can <c>return value;</c> directly.
    /// </summary>
    /// <param name="value">The value to lift.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Lifts a single diagnostic into a failed result, so a method can <c>return Diagnostic.Error(...);</c> directly.
    /// </summary>
    /// <param name="error">The diagnostic describing the failure.</param>
    public static implicit operator Result<T>(Diagnostic error) => Failure(error);
}
