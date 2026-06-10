using NSchema.Diff.Model;

namespace NSchema.Tests.Diff.Model;

public sealed class SchemaDiffTests
{
    [Fact]
    public void EnumerateObjects_YieldsEveryKindOnce()
    {
        // Kind-agnostic consumers (GetSummary, the destructive policy) rely on this covering every per-kind
        // collection — a new object kind must be added here, which this test makes loud.
        var diff = new SchemaDiff("app",
            Tables: [new TableDiff("app", "users", ChangeKind.Add)],
            Views: [new ViewDiff("app", "v", ChangeKind.Add)],
            Enums: [new EnumDiff("app", "e", ChangeKind.Add)],
            Sequences: [new SequenceDiff("app", "q", ChangeKind.Add)],
            Functions: [new FunctionDiff("app", "f", ChangeKind.Add)],
            Procedures: [new ProcedureDiff("app", "p", ChangeKind.Add)]);

        diff.EnumerateObjects().Select(o => o.Name).ShouldBe(["users", "v", "e", "q", "f", "p"]);
    }

    [Fact]
    public void GetSummary_CountsEveryObjectKind()
    {
        var diff = new DatabaseDiff([
            new SchemaDiff("app",
                Tables: [new TableDiff("app", "users", ChangeKind.Add)],
                Views: [new ViewDiff("app", "v", ChangeKind.Modify)],
                Enums: [new EnumDiff("app", "e", ChangeKind.Remove)],
                Sequences: [new SequenceDiff("app", "q", ChangeKind.Add)],
                Functions: [new FunctionDiff("app", "f", ChangeKind.Modify)],
                Procedures: [new ProcedureDiff("app", "p", ChangeKind.Remove)]),
        ]);

        diff.GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 2, Removed: 2));
    }
}
