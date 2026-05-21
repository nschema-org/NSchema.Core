namespace NSchema.Migration;

public sealed record SqlPlan(IReadOnlyList<string> Statements)
{
    public bool IsEmpty => Statements.Count == 0;
}
