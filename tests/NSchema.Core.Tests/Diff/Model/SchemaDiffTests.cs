using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Enums;
using NSchema.Diff.Domain.Models.Indexes;
using NSchema.Diff.Domain.Models.Routines;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Sequences;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models.Views;
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
        var diff = new SchemaDiff(new SqlIdentifier("app"),
            Tables: [new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Add)],
            Views: [new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("v"), ChangeKind.Add)],
            Enums: [new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("e"), ChangeKind.Add)],
            Sequences: [new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("q"), ChangeKind.Add)],
            Routines:
            [
                new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("f"), ChangeKind.Add, RoutineKind.Function),
                new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("p"), ChangeKind.Add, RoutineKind.Procedure),
            ]);

        diff.EnumerateObjects().Select(o => o.Name).ShouldBe(["users", "v", "e", "q", "f", "p"]);
    }

    [Fact]
    public void EnumerateMembers_YieldsEveryTableMemberKindOnce()
    {
        // Kind-agnostic consumers (GetSummary, the destructive policy) rely on this covering every member
        // collection — a new member kind must be added here, which this test makes loud.
        var table = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify,
            Columns: [new ColumnDiff(new SqlIdentifier("id"), ChangeKind.Add, null, null, null, null, null, null, null)],
            Indexes: [new IndexDiff(ChangeKind.Add, new SqlIdentifier("ix"), null, null)],
            PrimaryKey: [new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("pk"), null)],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Add, new SqlIdentifier("fk"), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("uq"), null)],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("ck"), null)]);

        table.EnumerateMembers().Select(m => m.Name).ShouldBe(["id", "ix", "pk", "fk", "uq", "ck"]);
    }

    [Fact]
    public void GetSummary_CountsEveryObjectKind()
    {
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"),
                Tables: [new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Add)],
                Views: [new ViewDiff(new SqlIdentifier("app"), new SqlIdentifier("v"), ChangeKind.Modify)],
                Enums: [new EnumDiff(new SqlIdentifier("app"), new SqlIdentifier("e"), ChangeKind.Remove)],
                Sequences: [new SequenceDiff(new SqlIdentifier("app"), new SqlIdentifier("q"), ChangeKind.Add)],
                Routines:
                [
                    new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("f"), ChangeKind.Modify, RoutineKind.Function),
                    new RoutineDiff(new SqlIdentifier("app"), new SqlIdentifier("p"), ChangeKind.Remove, RoutineKind.Procedure),
                ]),
        ]);

        diff.GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 2, Removed: 2));
    }
}
