using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// The non-generic base shared by every <see cref="Result{T}"/>.
/// </summary>
public abstract class Result
{
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

/// <summary>
/// The outcome of an operation that yields a <typeparamref name="T"/> on success or fails with reasons.
/// </summary>
/// <typeparam name="T">The value produced on success.</typeparam>
public sealed class Result<T> : Result
{
    internal Result(T? value, IReadOnlyList<Diagnostic> diagnostics)
    {
        Value = value;
        Diagnostics = diagnostics;
    }

    /// <summary>
    /// The produced value; non-null on success, and sometimes on a failure when the operation produced data despite finding errors.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Whether the operation succeeded; true if there are no errors.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public bool IsSuccess => !Errors.Any();

    /// <summary>
    /// Whether the operation failed; true if there are errors.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Every finding produced, of any severity. Empty on a clean success.
    /// </summary>
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    /// <summary>
    /// The error-severity subset of <see cref="Diagnostics"/>.
    /// </summary>
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    /// <summary>
    /// Returns the value of the result, or throws an exception if it's missing.
    /// </summary>
    /// <exception cref="InvalidOperationException">The result carried no value.</exception>
    public T Require() => Value ?? throw new InvalidOperationException($"The result was required to carry a value but did not. Diagnostics: {string.Join("; ", Diagnostics.Select(d => d.Message))}");

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
            ? Success(map(Value), Diagnostics.ToArray())
            : Failure<TOut>(Diagnostics);

    /// <summary>
    /// Lifts a value into a successful result, so a method can <c>return value;</c> directly.
    /// </summary>
    /// <param name="value">The value to lift.</param>
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Lifts a single diagnostic into a result, so a method can <c>return Diagnostic.Error(...);</c> directly
    /// (a failure when the diagnostic is an error).
    /// </summary>
    /// <param name="diagnostic">The diagnostic to carry.</param>
    public static implicit operator Result<T>(Diagnostic diagnostic) => Failure<T>(diagnostic);
}
