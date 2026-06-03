namespace NSchema.Migration.Diff.Model;

/// <summary>
/// Represents a change to a single value, capturing its previous and new state.
/// </summary>
/// <typeparam name="T">The type of the value that changed.</typeparam>
/// <param name="Old">The value before the change, or <see langword="null"/> if there was none.</param>
/// <param name="New">The value after the change, or <see langword="null"/> if it was removed.</param>
public sealed record ValueChange<T>(T? Old, T? New);
