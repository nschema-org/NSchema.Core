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
using NSchema.Model.Constraints;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Plan.Model.Services;

namespace NSchema.Tests.Plan;

/// <summary>
/// Snapshot coverage for <see cref="PlanLinearizer"/>. Ordering is the linearizer's whole
/// contract, and it reads most clearly as a flat ordered list — so this pins the emitted action sequence
/// for a diff that touches schemas, tables, columns, indexes, and constraints. The fine-grained mapping
/// and priority assertions stay in <see cref="PlanLinearizerTests"/>.
///
/// Each action is projected as <c>{ Type, Action }</c> because the action list is polymorphic: without the
/// concrete type name many records are ambiguous (e.g. CreateSchema vs CreateTable both show just a schema).
/// </summary>
public sealed class PlanLinearizerSnapshotTests
{
    private readonly PlanLinearizer _linearizer = new();

    [Fact]
    public Task Linearize_RichDiff_OrdersActionsSafely()
    {
        // A new schema; a newly-added table (columns + PK carried inline on Definition, with a separate
        // index and grant); a modified table (add/drop/retype columns, new index, dropped FK); two added
        // views (one reading the other), a renamed view, and a dropped view; a dropped schema carrying its own
        // table, view, enum and sequence (all dropped before the schema). Enough cross-kind work to exercise the
        // priority ordering and the view dependency sort.
        var newTable = new TableDiff("app", "users", ChangeKind.Add, null, null,
            Columns: [],
            Grants: [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, "users_name_ix", new TableIndex { Name = "users_name_ix", Columns = ["name"], IsUnique = true }, null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint { Name = "users_email_uq", ColumnNames = ["email"] })],
            Definition: new Table
            {
                Name = "users",
                PrimaryKey = new PrimaryKey { Name = "users_pkey", ColumnNames = ["id"] },
                Columns = [
                    new Column { Name = "id", Type = SqlType.BigInt, IsIdentity = true, IdentityOptions = new IdentityOptions(1, 1, 1) },
                    new Column { Name = "name", Type = SqlType.VarChar(255) },
                ],
            });

        var modifiedTable = new TableDiff("app", "orders", ChangeKind.Modify, "purchases", null,
            Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff("notes", ChangeKind.Add, new Column { Name = "notes", Type = SqlType.Text, IsNullable = true }, null, null, null, null, null, null),
                new ColumnDiff("total_label", ChangeKind.Modify, Generated: new ValueChange<SqlText>(null, "total::text")),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, new Column { Name = "legacy_flag", Type = SqlType.Boolean }, null, null, null, null, null, null),
            ],
            Grants: [],
            Indexes: [new IndexDiff(ChangeKind.Add, "orders_total_ix",
                new TableIndex { Name = "orders_total_ix", Columns = [new IndexColumn("total", Sort: IndexSort.Descending)], Method = "btree", Include = ["code"] }, null)],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "orders_code_uq", new UniqueConstraint { Name = "orders_code_uq", ColumnNames = ["code"] })],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, "orders_total_chk", new CheckConstraint { Name = "orders_total_chk", Expression = "total >= 0" })],
            ExclusionConstraints: [new ExclusionConstraintDiff(ChangeKind.Add, "orders_slot_excl",
                new ExclusionConstraint { Name = "orders_slot_excl", Elements = [new ExclusionElement("&&", "slot")], Method = "gist" })]);

        // Listed dependent-first on purpose: the dependency sort must reorder them so user_summary (which
        // reads active_users) is created after it.
        var views = new ViewDiff[]
        {
            new("app", "user_summary", ChangeKind.Add,
                Definition: new View { Name = "user_summary", Body = "SELECT * FROM app.active_users", DependsOn = [new ViewDependency("app", "active_users")] },
                DependsOn: [new ViewDependency("app", "active_users")]),
            new("app", "active_users", ChangeKind.Add,
                Definition: new View { Name = "active_users", Body = "SELECT * FROM app.users", DependsOn = [new ViewDependency("app", "users")] },
                DependsOn: [new ViewDependency("app", "users")]),
            new("app", "report", ChangeKind.Modify, RenamedFrom: "legacy_report"),
            new("app", "stale_view", ChangeKind.Remove),
        };

        // Enums and sequences: additions (created before tables), an anchored value addition, a rename,
        // an options change, and drops (after tables, before the schema drop).
        var enums = new EnumDiff[]
        {
            new("app", "order_status", ChangeKind.Add, Definition: new EnumType { Name = "order_status", Values = ["pending", "shipped"] }),
            new("app", "priority", ChangeKind.Modify, RenamedFrom: "importance",
                AddedValues: [new EnumValueAddition("medium", After: "low")]),
            new("app", "stale_enum", ChangeKind.Remove),
        };
        var sequences = new SequenceDiff[]
        {
            new("app", "order_id", ChangeKind.Add, Definition: new Sequence { Name = "order_id", Options = new SequenceOptions(StartWith: 100) }),
            new("app", "ticket_id", ChangeKind.Modify,
                Options: new ValueChange<SequenceOptions>(new SequenceOptions(StartWith: 1), new SequenceOptions(StartWith: 1000))),
            new("app", "stale_seq", ChangeKind.Remove),
        };

        // Routines: an add, a rename + signature change (rename then recreate), drops, and a procedure.
        var routines = new RoutineDiff[]
        {
            new("app", "add_tax", ChangeKind.Add, RoutineKind.Function,
                Definition: new Routine { Name = "add_tax", RoutineKind = RoutineKind.Function, Arguments = "amount numeric", Definition = "RETURNS numeric AS $$ SELECT amount $$" }),
            new("app", "score", ChangeKind.Modify, RoutineKind.Function, RenamedFrom: "old_score",
                Definition: new Routine { Name = "score", RoutineKind = RoutineKind.Function, Arguments = "user_id bigint, weight numeric", Definition = "RETURNS numeric AS $$ SELECT 1 $$" },
                Arguments: new ValueChange<SqlText>("user_id bigint", "user_id bigint, weight numeric")),
            new("app", "stale_fn", ChangeKind.Remove, RoutineKind.Function),
            new("app", "archive", ChangeKind.Add, RoutineKind.Procedure,
                Definition: new Routine { Name = "archive", RoutineKind = RoutineKind.Procedure, Arguments = "before date", Definition = "LANGUAGE sql AS $$ DELETE $$" }),
            new("app", "stale_proc", ChangeKind.Remove, RoutineKind.Procedure),
        };

        var diff = new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("reporting", ChangeKind.Add, null, null, [], []),
                new SchemaDiff("app", null, null, null, [], [newTable, modifiedTable], views, enums, sequences, routines),
                new SchemaDiff("scratch", ChangeKind.Remove, null, null, [],
                    [new TableDiff("scratch", "temp_data", ChangeKind.Remove)],
                    [new ViewDiff("scratch", "temp_view", ChangeKind.Remove)],
                    [new EnumDiff("scratch", "temp_status", ChangeKind.Remove)],
                    [new SequenceDiff("scratch", "temp_seq", ChangeKind.Remove)]),
            ]);

        var plan = _linearizer.Linearize(diff);

        return Verify(plan.Select(a => new { Type = a.GetType().Name, Action = a }));
    }
}
