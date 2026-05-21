namespace NSchema.Migration;

public sealed record StatementPlan(IReadOnlyList<string> Statements)
{
    public bool IsEmpty => Statements.Count == 0;
}
