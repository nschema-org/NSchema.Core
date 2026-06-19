using NSchema.Diff.Model;
using NSchema.Sql.Model;

namespace NSchema.Operations;

/// <summary>
/// Formats the one-line recap an operation reports when it finishes, so the outcome line carries what
/// changed rather than a content-free "completed successfully" — CI logs are read bottom-up. The vocabulary
/// (added / changed / destroyed) matches the diff renderer's <c>Plan:</c> footer.
/// </summary>
internal static class RunSummary
{
    /// <summary>
    /// Describes the changes in <paramref name="diff"/> ("3 added, 1 changed, 2 destroyed"), omitting any
    /// category with no changes, or "no changes" when the diff is empty.
    /// </summary>
    public static string Describe(DatabaseDiff diff)
    {
        var (added, modified, removed) = diff.GetSummary();

        var changes = new List<string>(3);
        if (added > 0)
        {
            changes.Add($"{added} added");
        }

        if (modified > 0)
        {
            changes.Add($"{modified} changed");
        }

        if (removed > 0)
        {
            changes.Add($"{removed} destroyed");
        }

        return changes.Count > 0 ? string.Join(", ", changes) : "no changes";
    }

    /// <summary>
    /// Describes the changes in <paramref name="diff"/> together with the number of SQL statements that ran,
    /// e.g. "3 added, 1 changed (14 statements)".
    /// </summary>
    public static string Describe(DatabaseDiff diff, SqlPlan sql)
    {
        var count = sql.Statements.Count;
        var statements = count == 1 ? "1 statement" : $"{count} statements";
        return $"{Describe(diff)} ({statements})";
    }

    /// <summary>
    /// A verbose preview of what execution will run: the statement count and how many must run outside a
    /// transaction (e.g. <c>CREATE INDEX CONCURRENTLY</c>) — the atomicity-breakers worth knowing about when
    /// diagnosing a partial apply.
    /// </summary>
    public static string DescribeExecution(SqlPlan sql)
    {
        var count = sql.Statements.Count;
        var statements = count == 1 ? "1 statement" : $"{count} statements";

        var outside = 0;
        foreach (var statement in sql.Statements)
        {
            if (statement.RunOutsideTransaction)
            {
                outside++;
            }
        }

        return outside == 0
            ? $"Executing {statements}."
            : $"Executing {statements} ({outside} must run outside a transaction).";
    }
}
