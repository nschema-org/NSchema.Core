namespace NSchema.Diff.Model.Services;

/// <summary>
/// Helpers for building <see cref="ValueChange{T}"/> instances from a current/desired pair.
/// </summary>
internal static class ValueChanges
{
    /// <summary>
    /// Returns the change from <paramref name="current"/> to <paramref name="desired"/>, or
    /// <see langword="null"/> when the two are equal (no change to record).
    /// </summary>
    public static ValueChange<T>? Changed<T>(T? current, T? desired) where T : class =>
        EqualityComparer<T>.Default.Equals(current, desired) ? null : new ValueChange<T>(current, desired);
}
