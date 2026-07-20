using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Enums;
using NSchema.Diff.Model.Indexes;
using NSchema.Diff.Model.Routines;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Sequences;
using NSchema.Diff.Model.Tables;
using NSchema.Diff.Model.Views;
using NSchema.Model;
using NSchema.Model.Routines;

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
            Routines:
            [
                new RoutineDiff("app", "f", ChangeKind.Add, RoutineKind.Function),
                new RoutineDiff("app", "p", ChangeKind.Add, RoutineKind.Procedure),
            ]);

        diff.EnumerateObjects().Select(o => o.Name).ShouldBe(["users", "v", "e", "q", "f", "p"]);
    }

    [Fact]
    public void EnumerateMembers_YieldsEveryTableMemberKindOnce()
    {
        // Kind-agnostic consumers (GetSummary, the destructive policy) rely on this covering every member
        // collection — a new member kind must be added here, which this test makes loud.
        var table = new TableDiff("app", "users", ChangeKind.Modify,
            Columns: [new ColumnDiff("id", ChangeKind.Add, null, null, null, null, null, null, null)],
            Indexes: [new IndexDiff(ChangeKind.Add, "ix", null, null)],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Add, "pk", null)],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Add, "fk", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "uq", null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, "ck", null)]);

        table.EnumerateMembers().Select(m => m.Name).ShouldBe(["id", "ix", "pk", "fk", "uq", "ck"]);
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
                Routines:
                [
                    new RoutineDiff("app", "f", ChangeKind.Modify, RoutineKind.Function),
                    new RoutineDiff("app", "p", ChangeKind.Remove, RoutineKind.Procedure),
                ]),
        ]);

        diff.GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 2, Removed: 2));
    }
}
