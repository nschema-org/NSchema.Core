using System.Diagnostics.CodeAnalysis;

namespace NSchema.Diagnostics;

/// <summary>
/// The outcome of an operation that yields a <typeparamref name="T"/> on success or fails with reasons.
/// </summary>
/// <typeparam name="T">The value produced on success.</typeparam>
public sealed class Result<T> : Result
{
    internal Result(T? value, IReadOnlyList<Diagnostic> diagnostics) : base(diagnostics) => Value = value;

    /// <summary>
    /// The produced value; non-null on success, and sometimes on a failure when the operation produced data despite finding errors.
    /// </summary>
    public T? Value { get; }

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool IsSuccess => base.IsSuccess;

    /// <inheritdoc/>
    [MemberNotNullWhen(false, nameof(Value))]
    public override bool IsFailure => base.IsFailure;

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
            ? Result.Success<TOut>(map(Value), Diagnostics.ToArray())
            : Result.Failure<TOut>(Diagnostics);

    /// <summary>
    /// Lifts a value into a successful result, so a method can <c>return value;</c> directly.
    /// </summary>
    /// <param name="value">The value to lift.</param>
    public static implicit operator Result<T>(T value) => Result.Success<T>(value);

    /// <summary>
    /// Lifts a single diagnostic into a result, so a method can <c>return Diagnostic.Error(...);</c> directly
    /// (a failure when the diagnostic is an error).
    /// </summary>
    /// <param name="diagnostic">The diagnostic to carry.</param>
    public static implicit operator Result<T>(Diagnostic diagnostic) => Result.Failure<T>(diagnostic);
}
