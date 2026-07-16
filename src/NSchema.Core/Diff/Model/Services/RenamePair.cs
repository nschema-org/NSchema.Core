using NSchema.Model;

namespace NSchema.Diff.Domain;

/// <summary>
/// A rename hint for the comparer's matching: the entity currently named <paramref name="From"/> is the one
/// declared as <paramref name="To"/>, within whatever container the match runs in.
/// </summary>
internal readonly record struct RenamePair(SqlIdentifier From, SqlIdentifier To);
