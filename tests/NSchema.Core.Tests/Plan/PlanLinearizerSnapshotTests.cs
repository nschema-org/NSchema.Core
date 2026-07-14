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
using NSchema.Plan.Domain;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Enums;
using NSchema.Project.Domain.Models.Indexes;
using NSchema.Project.Domain.Models.Routines;
using NSchema.Project.Domain.Models.Sequences;
using NSchema.Project.Domain.Models.Tables;
using NSchema.Project.Domain.Models.Views;

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
        var newTable = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Add, null, null,
            Columns: [],
            Grants: [new GrantChange(ChangeKind.Add, new SqlIdentifier("readers"), TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, new SqlIdentifier("users_name_ix"), new TableIndex(new SqlIdentifier("users_name_ix"), ["name"], IsUnique: true), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]))],
            Definition: new Table(new SqlIdentifier("users"),
                PrimaryKey: new PrimaryKey(new SqlIdentifier("users_pkey"), [new SqlIdentifier("id")]),
                Columns:
                [
                    new Column(new SqlIdentifier("id"), SqlType.BigInt, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1)),
                    new Column(new SqlIdentifier("name"), SqlType.VarChar(255)),
                ]));

        var modifiedTable = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("orders"), ChangeKind.Modify, new SqlIdentifier("purchases"), null,
            Columns:
            [
                new ColumnDiff(new SqlIdentifier("total"), ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff(new SqlIdentifier("notes"), ChangeKind.Add, new Column(new SqlIdentifier("notes"), SqlType.Text, IsNullable: true), null, null, null, null, null, null),
                new ColumnDiff(new SqlIdentifier("total_label"), ChangeKind.Modify, Generated: new ValueChange<SqlText>(null, new SqlText("total::text"))),
                new ColumnDiff(new SqlIdentifier("legacy_flag"), ChangeKind.Remove, new Column(new SqlIdentifier("legacy_flag"), SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [],
            Indexes: [new IndexDiff(ChangeKind.Add, new SqlIdentifier("orders_total_ix"),
                new TableIndex(new SqlIdentifier("orders_total_ix"), [new IndexColumn(new SqlIdentifier("total"), Sort: IndexSort.Descending)], Method: "btree", Include: [new SqlIdentifier("code")]), null)],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, new SqlIdentifier("orders_user_fk"), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("orders_code_uq"), new UniqueConstraint(new SqlIdentifier("orders_code_uq"), [new SqlIdentifier("code")]))],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("orders_total_chk"), new CheckConstraint(new SqlIdentifier("orders_total_chk"), new SqlText("total >= 0")))],
            ExclusionConstraints: [new ExclusionConstraintDiff(ChangeKind.Add, new SqlIdentifier("orders_slot_excl"),
                new ExclusionConstraint(new SqlIdentifier("orders_slot_excl"), [new ExclusionElement("&&", new SqlIdentifier("slot"))], "gist"))]);

        // Listed dependent-first on purpose: the dependency sort must reorder them so user_summary (which
        // reads active_users) is created after it.
        var views = new ViewDiff[]
        {
            new(new SqlIdentifier("app"), new SqlIdentifier("user_summary"), ChangeKind.Add,
                Definition: new View(new SqlIdentifier("user_summary"), new SqlText("SELECT * FROM app.active_users"), DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("active_users"))]),
                DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("active_users"))]),
            new(new SqlIdentifier("app"), new SqlIdentifier("active_users"), ChangeKind.Add,
                Definition: new View(new SqlIdentifier("active_users"), new SqlText("SELECT * FROM app.users"), DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))]),
                DependsOn: [new ViewDependency(new SqlIdentifier("app"), new SqlIdentifier("users"))]),
            new(new SqlIdentifier("app"), new SqlIdentifier("report"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("legacy_report")),
            new(new SqlIdentifier("app"), new SqlIdentifier("stale_view"), ChangeKind.Remove),
        };

        // Enums and sequences: additions (created before tables), an anchored value addition, a rename,
        // an options change, and drops (after tables, before the schema drop).
        var enums = new EnumDiff[]
        {
            new(new SqlIdentifier("app"), new SqlIdentifier("order_status"), ChangeKind.Add, Definition: new EnumType(new SqlIdentifier("order_status"), ["pending", "shipped"])),
            new(new SqlIdentifier("app"), new SqlIdentifier("priority"), ChangeKind.Modify, RenamedFrom: new SqlIdentifier("importance"),
                AddedValues: [new EnumValueAddition("medium", After: "low")]),
            new(new SqlIdentifier("app"), new SqlIdentifier("stale_enum"), ChangeKind.Remove),
        };
        var sequences = new SequenceDiff[]
        {
            new(new SqlIdentifier("app"), new SqlIdentifier("order_id"), ChangeKind.Add, Definition: new Sequence(new SqlIdentifier("order_id"), new SequenceOptions(StartWith: 100))),
            new(new SqlIdentifier("app"), new SqlIdentifier("ticket_id"), ChangeKind.Modify,
                Options: new ValueChange<SequenceOptions>(new SequenceOptions(StartWith: 1), new SequenceOptions(StartWith: 1000))),
            new(new SqlIdentifier("app"), new SqlIdentifier("stale_seq"), ChangeKind.Remove),
        };

        // Routines: an add, a rename + signature change (rename then recreate), drops, and a procedure.
        var routines = new RoutineDiff[]
        {
            new(new SqlIdentifier("app"), new SqlIdentifier("add_tax"), ChangeKind.Add, RoutineKind.Function,
                Definition: new Routine(new SqlIdentifier("add_tax"), RoutineKind.Function, new SqlText("amount numeric"), new SqlText("RETURNS numeric AS $$ SELECT amount $$"))),
            new(new SqlIdentifier("app"), new SqlIdentifier("score"), ChangeKind.Modify, RoutineKind.Function, RenamedFrom: new SqlIdentifier("old_score"),
                Definition: new Routine(new SqlIdentifier("score"), RoutineKind.Function, new SqlText("user_id bigint, weight numeric"), new SqlText("RETURNS numeric AS $$ SELECT 1 $$")),
                Arguments: new ValueChange<SqlText>(new SqlText("user_id bigint"), new SqlText("user_id bigint, weight numeric"))),
            new(new SqlIdentifier("app"), new SqlIdentifier("stale_fn"), ChangeKind.Remove, RoutineKind.Function),
            new(new SqlIdentifier("app"), new SqlIdentifier("archive"), ChangeKind.Add, RoutineKind.Procedure,
                Definition: new Routine(new SqlIdentifier("archive"), RoutineKind.Procedure, new SqlText("before date"), new SqlText("LANGUAGE sql AS $$ DELETE $$"))),
            new(new SqlIdentifier("app"), new SqlIdentifier("stale_proc"), ChangeKind.Remove, RoutineKind.Procedure),
        };

        var diff = new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff(new SqlIdentifier("reporting"), ChangeKind.Add, null, null, [], []),
                new SchemaDiff(new SqlIdentifier("app"), null, null, null, [], [newTable, modifiedTable], views, enums, sequences, routines),
                new SchemaDiff(new SqlIdentifier("scratch"), ChangeKind.Remove, null, null, [],
                    [new TableDiff(new SqlIdentifier("scratch"), new SqlIdentifier("temp_data"), ChangeKind.Remove)],
                    [new ViewDiff(new SqlIdentifier("scratch"), new SqlIdentifier("temp_view"), ChangeKind.Remove)],
                    [new EnumDiff(new SqlIdentifier("scratch"), new SqlIdentifier("temp_status"), ChangeKind.Remove)],
                    [new SequenceDiff(new SqlIdentifier("scratch"), new SqlIdentifier("temp_seq"), ChangeKind.Remove)]),
            ]);

        var plan = _linearizer.Linearize(diff);

        return Verify(plan.Select(a => new { Type = a.GetType().Name, Action = a }));
    }
}
