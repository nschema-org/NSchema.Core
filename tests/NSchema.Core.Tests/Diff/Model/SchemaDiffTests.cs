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
using NSchema.Model.Columns;
using NSchema.Model.Enums;
using NSchema.Model.Sequences;
using NSchema.Model.Views;
using NSchema.Model.Constraints;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Tables;

namespace NSchema.Tests.Diff.Model;

public sealed class SchemaDiffTests
{
    [Fact]
    public void EnumerateObjects_YieldsEveryKindOnce()
    {
        // Kind-agnostic consumers (GetSummary, the destructive policy) rely on this covering every per-kind
        // collection — a new object kind must be added here, which this test makes loud.
        var diff = SchemaDiff.Containing("app") with
        {
            Tables = [TableDiff.Added("app", new Table { Name = "users" })],
            Views = [ViewDiff.Added("app", new View { Name = "v", Body = "SELECT 1" })],
            Enums = [EnumDiff.Added("app", new EnumType { Name = "e", Values = ["a"] })],
            Sequences = [SequenceDiff.Added("app", new Sequence { Name = "q" })],
            Routines = [
                RoutineDiff.Added("app", new Routine { Name = "f", RoutineKind = RoutineKind.Function, Arguments = "", Definition = "BEGIN END" }),
                RoutineDiff.Added("app", new Routine { Name = "p", RoutineKind = RoutineKind.Procedure, Arguments = "", Definition = "BEGIN END" }),
            ],
        };

        diff.EnumerateObjects().Select(o => o.Name).ShouldBe(["users", "v", "e", "q", "f", "p"]);
    }

    [Fact]
    public void EnumerateMembers_YieldsEveryTableMemberKindOnce()
    {
        // Kind-agnostic consumers (GetSummary, the destructive policy) rely on this covering every member
        // collection — a new member kind must be added here, which this test makes loud.
        var table = TableDiff.Modified("app", "users") with
        {
            Columns = [ColumnDiff.Added(new Column { Name = "id", Type = SqlType.Int })],
            Indexes = [IndexDiff.Added(new TableIndex { Name = "ix", Columns = ["id"] })],
            PrimaryKeys = [PrimaryKeyDiff.Added(new PrimaryKey { Name = "pk", ColumnNames = ["id"] })],
            ForeignKeys = [ForeignKeyDiff.Added(new ForeignKey { Name = "fk", ColumnNames = ["id"], References = new ObjectAddress("app", "other"), ReferencedColumnNames = ["id"] })],
            UniqueConstraints = [UniqueConstraintDiff.Added(new UniqueConstraint { Name = "uq", ColumnNames = ["id"] })],
            Checks = [CheckConstraintDiff.Added(new CheckConstraint { Name = "ck", Expression = "id > 0" })],
        };

        table.EnumerateMembers().Select(m => m.Name).ShouldBe(["id", "ix", "pk", "fk", "uq", "ck"]);
    }

    [Fact]
    public void GetSummary_CountsEveryObjectKind()
    {
        var diff = new DatabaseDiff([
            SchemaDiff.Containing("app") with
            {
                Tables = [TableDiff.Added("app", new Table { Name = "users" })],
                Views = [ViewDiff.Modified("app", "v")],
                Enums = [EnumDiff.Removed("app", "e")],
                Sequences = [SequenceDiff.Added("app", new Sequence { Name = "q" })],
                Routines = [
                    RoutineDiff.Modified("app", "f", RoutineKind.Function),
                    RoutineDiff.Removed("app", "p", RoutineKind.Procedure),
                ],
            },
        ]);

        diff.GetSummary().ShouldBe(new DiffSummary(Added: 2, Modified: 2, Removed: 2));
    }
}
