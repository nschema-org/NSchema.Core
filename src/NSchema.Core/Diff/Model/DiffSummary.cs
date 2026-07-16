namespace NSchema.Diff.Model;

/// <summary>
/// Aggregate counts of the top-level changes in a <see cref="DatabaseDiff"/>. Each changed schema and each
/// changed table contributes one to the count for its <see cref="ChangeKind"/>.
/// </summary>
/// <param name="Added">The number of schemas and tables being created.</param>
/// <param name="Modified">The number of schemas and tables being modified.</param>
/// <param name="Removed">The number of schemas and tables being removed.</param>
public sealed record DiffSummary(int Added, int Modified, int Removed);
