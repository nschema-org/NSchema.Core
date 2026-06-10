using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Schema.Model;

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
public sealed class LinearizerSnapshotTests
{
    private readonly PlanLinearizer _linearizer = new();

    [Fact]
    public Task Linearize_RichDiff_OrdersActionsSafely()
    {
        // A new schema; a newly-added table (columns + PK carried inline on Definition, with a separate
        // index and grant); a modified table (add/drop/retype columns, new index, dropped FK); two added
        // views (one reading the other), a renamed view, and a dropped view; a dropped schema. Enough
        // cross-kind work to exercise the priority ordering and the view dependency sort.
        var newTable = new TableDiff("app", "users", ChangeKind.Add, null, null,
            Columns: [],
            Grants: [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, "users_name_ix", new TableIndex("users_name_ix", ["name"], IsUnique: true), null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"]))],
            Definition: new Table("users",
                PrimaryKey: new PrimaryKey("users_pkey", ["id"]),
                Columns:
                [
                    new Column("id", SqlType.BigInt, IsIdentity: true, IdentityOptions: new IdentityOptions(1, 1, 1)),
                    new Column("name", SqlType.VarChar(255)),
                ]));

        var modifiedTable = new TableDiff("app", "orders", ChangeKind.Modify, "purchases", null,
            Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff("notes", ChangeKind.Add, new Column("notes", SqlType.Text, IsNullable: true), null, null, null, null, null, null),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, new Column("legacy_flag", SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [],
            Indexes: [new IndexDiff(ChangeKind.Add, "orders_total_ix", new TableIndex("orders_total_ix", ["total"]), null)],
            ForeignKeys: [new ForeignKeyDiff(ChangeKind.Remove, "orders_user_fk", null)],
            UniqueConstraints: [new UniqueConstraintDiff(ChangeKind.Add, "orders_code_uq", new UniqueConstraint("orders_code_uq", ["code"]))],
            Checks: [new CheckConstraintDiff(ChangeKind.Add, "orders_total_chk", new CheckConstraint("orders_total_chk", "total >= 0"))]);

        // Listed dependent-first on purpose: the dependency sort must reorder them so user_summary (which
        // reads active_users) is created after it.
        var views = new ViewDiff[]
        {
            new("app", "user_summary", ChangeKind.Add,
                Definition: new View("user_summary", "SELECT * FROM app.active_users", DependsOn: [new ViewDependency("app", "active_users")]),
                DependsOn: [new ViewDependency("app", "active_users")]),
            new("app", "active_users", ChangeKind.Add,
                Definition: new View("active_users", "SELECT * FROM app.users", DependsOn: [new ViewDependency("app", "users")]),
                DependsOn: [new ViewDependency("app", "users")]),
            new("app", "report", ChangeKind.Modify, RenamedFrom: "legacy_report"),
            new("app", "stale_view", ChangeKind.Remove),
        };

        var diff = new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("reporting", ChangeKind.Add, null, null, [], []),
                new SchemaDiff("app", null, null, null, [], [newTable, modifiedTable], views),
                new SchemaDiff("scratch", ChangeKind.Remove, null, null, [], []),
            ]);

        var plan = _linearizer.Linearize(diff);

        return Verify(plan.Select(a => new { Type = a.GetType().Name, Action = a }));
    }
}
