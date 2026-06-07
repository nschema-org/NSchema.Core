using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Schema.Model;

namespace NSchema.Tests.Plan;

/// <summary>
/// Snapshot coverage for <see cref="DefaultPlanLinearizer"/>. Ordering is the linearizer's whole
/// contract, and it reads most clearly as a flat ordered list — so this pins the emitted action sequence
/// for a diff that touches schemas, tables, columns, indexes, and constraints. The fine-grained mapping
/// and priority assertions stay in <see cref="DefaultPlanLinearizerTests"/>.
///
/// Each action is projected as <c>{ Type, Action }</c> because the action list is polymorphic: without the
/// concrete type name many records are ambiguous (e.g. CreateSchema vs CreateTable both show just a schema).
/// </summary>
public sealed class DefaultMigrationLinearizerSnapshotTests
{
    private readonly DefaultPlanLinearizer _linearizer = new();

    [Fact]
    public Task Linearize_RichDiff_OrdersActionsSafely()
    {
        // A new schema; a newly-added table (columns + PK carried inline on Definition, with a separate
        // index and grant); a modified table (add/drop/retype columns, new index, dropped FK); a dropped
        // schema. Enough cross-kind work to exercise the priority ordering.
        var newTable = new TableDiff("app", "users", ChangeKind.Add, null, null,
            Columns: [],
            Grants: [new GrantChange(ChangeKind.Add, "readers", TablePrivilege.Select)],
            Indexes: [new IndexDiff(ChangeKind.Add, "users_name_ix", TableIndex.Create("users_name_ix", ["name"], isUnique: true), null)],
            Constraints: [],
            Definition: Table.Create("users",
                primaryKey: new PrimaryKey("users_pkey", ["id"]),
                columns:
                [
                    Column.Create("id", SqlType.BigInt, isIdentity: true, identityOptions: new IdentityOptions(1, 1, 1)),
                    Column.Create("name", SqlType.VarChar(255)),
                ]));

        var modifiedTable = new TableDiff("app", "orders", ChangeKind.Modify, "purchases", null,
            Columns:
            [
                new ColumnDiff("total", ChangeKind.Modify, null, null,
                    Type: new ValueChange<SqlType>(SqlType.Int, SqlType.BigInt),
                    Nullability: new ValueChange<bool>(true, false), Default: null, Identity: null, Comment: null),
                new ColumnDiff("notes", ChangeKind.Add, Column.Create("notes", SqlType.Text, isNullable: true), null, null, null, null, null, null),
                new ColumnDiff("legacy_flag", ChangeKind.Remove, Column.Create("legacy_flag", SqlType.Boolean), null, null, null, null, null, null),
            ],
            Grants: [],
            Indexes: [new IndexDiff(ChangeKind.Add, "orders_total_ix", TableIndex.Create("orders_total_ix", ["total"]), null)],
            Constraints: [new ConstraintDiff(ChangeKind.Remove, ConstraintType.ForeignKey, "orders_user_fk", null, null)]);

        var diff = new DatabaseDiff(
            Schemas:
            [
                new SchemaDiff("reporting", ChangeKind.Add, null, null, [], []),
                new SchemaDiff("app", null, null, null, [], [newTable, modifiedTable]),
                new SchemaDiff("scratch", ChangeKind.Remove, null, null, [], []),
            ]);

        var plan = _linearizer.Linearize(diff);

        return Verify(plan.Select(a => new { Type = a.GetType().Name, Action = a }));
    }
}
