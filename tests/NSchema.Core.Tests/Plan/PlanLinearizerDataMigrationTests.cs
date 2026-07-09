using NSchema.Diff.Model;
using NSchema.Plan;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.Constraints;
using NSchema.Plan.Model.Migrations;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Tables;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="PlanLinearizer"/>'s handling of diff nodes annotated with a matched
/// <see cref="DataMigration"/>: the backfill decomposition of a required column add, the ordering of
/// <see cref="ExecuteDataMigration"/> around type changes and constraint adds, and the field flow-through.
/// </summary>
public sealed class PlanLinearizerDataMigrationTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> LinearizeTable(TableDiff table)
        => _linearizer.Linearize(new DatabaseDiff([new SchemaDiff("app", Tables: [table])]));

    private IReadOnlyList<MigrationAction> LinearizeColumn(ColumnDiff column)
        => LinearizeTable(new TableDiff("app", "users", ChangeKind.Modify, Columns: [column]));

    private static DataMigration Migration(DataMigrationTrigger trigger, string member, string? name = null, string? sql = null) =>
        new(name, trigger, "app", "users", member, sql ?? $"UPDATE app.users -- {member}");

    [Fact]
    public void Linearize_AnnotatedRequiredColumnAdd_DecomposesIntoNullableAddBackfillAndTighten()
    {
        // Arrange — a NOT NULL, no-default column with a matched backfill cannot land in one step against a
        // populated table: it is added nullable, backfilled, then tightened.
        var migration = Migration(DataMigrationTrigger.AddColumn, "email", name: "backfill emails");
        var column = new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text)) { Migration = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(3);
        var add = plan[0].ShouldBeOfType<AddColumn>();
        add.Column.Name.ShouldBe("email");
        add.Column.IsNullable.ShouldBeTrue();
        var backfill = plan[1].ShouldBeOfType<ExecuteDataMigration>();
        backfill.Name.ShouldBe("backfill emails");
        backfill.Sql.ShouldBe(migration.Sql);
        var tighten = plan[2].ShouldBeOfType<AlterColumnNullability>();
        tighten.ColumnName.ShouldBe("email");
        tighten.OldNullable.ShouldBeTrue();
        tighten.NewNullable.ShouldBeFalse();
        tighten.ColumnType.ShouldBe(SqlType.Text);
    }

    [Fact]
    public void Linearize_AnnotatedNullableColumnAdd_EmitsPlainAddPlusMigration()
    {
        // Arrange — a nullable add needs no decomposition: the column lands as declared, then the migration runs.
        var migration = Migration(DataMigrationTrigger.AddColumn, "email");
        var column = new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, IsNullable: true)) { Migration = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.IsNullable.ShouldBeTrue();
        plan[1].ShouldBeOfType<ExecuteDataMigration>();
        plan.OfType<AlterColumnNullability>().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("defaulted")]
    [InlineData("identity")]
    [InlineData("generated")]
    public void Linearize_AnnotatedSelfFillingRequiredAdd_IsNotDecomposed(string shape)
    {
        // Arrange — defaults, identity, and generation fill existing rows themselves, so the add keeps its
        // declared NOT NULL shape and only the migration is appended.
        var definition = shape switch
        {
            "defaulted" => new Column("email", SqlType.Text, DefaultExpression: "''"),
            "identity" => new Column("email", SqlType.BigInt, IsIdentity: true),
            _ => new Column("email", SqlType.Text, GeneratedExpression: "lower(name)"),
        };
        var migration = Migration(DataMigrationTrigger.AddColumn, "email");
        var column = new ColumnDiff("email", ChangeKind.Add, definition) { Migration = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.ShouldBe(definition);
        plan[1].ShouldBeOfType<ExecuteDataMigration>();
        plan.OfType<AlterColumnNullability>().ShouldBeEmpty();
    }

    [Fact]
    public void Linearize_AnnotatedTypeChange_RunsMigrationBeforeAlterColumnType()
    {
        // Arrange — the migration's SQL prepares the data for the cast, so it must run first.
        var migration = Migration(DataMigrationTrigger.AlterColumnType, "total");
        var column = new ColumnDiff("total", ChangeKind.Modify,
            Type: new ValueChange<SqlType>(SqlType.Text, SqlType.Int)) { Migration = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        var prep = plan[0].ShouldBeOfType<ExecuteDataMigration>();
        prep.Trigger.ShouldBe(DataMigrationTrigger.AlterColumnType);
        var alter = plan[1].ShouldBeOfType<AlterColumnType>();
        alter.OldType.ShouldBe(SqlType.Text);
        alter.NewType.ShouldBe(SqlType.Int);
    }

    [Fact]
    public void Linearize_AnnotatedUniqueConstraintAdd_RunsMigrationBeforeAddUniqueConstraint()
    {
        // Arrange — the migration de-duplicates the data the constraint depends on.
        var migration = Migration(DataMigrationTrigger.AddConstraint, "users_email_uq");
        var constraint = new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq",
            new UniqueConstraint("users_email_uq", ["email"])) { Migration = migration };

        // Act
        var plan = LinearizeTable(new TableDiff("app", "users", ChangeKind.Modify, UniqueConstraints: [constraint]));

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<ExecuteDataMigration>().MemberName.ShouldBe("users_email_uq");
        plan[1].ShouldBeOfType<AddUniqueConstraint>().UniqueConstraint.Name.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_TwoAnnotatedChanges_KeepMigrationsInDiffOrder()
    {
        // Arrange — two annotated column adds; their migrations share a priority band, so the stable sort
        // preserves the diff's declaration order.
        var first = new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, IsNullable: true))
        {
            Migration = Migration(DataMigrationTrigger.AddColumn, "email", name: "first"),
        };
        var second = new ColumnDiff("phone", ChangeKind.Add, new Column("phone", SqlType.Text, IsNullable: true))
        {
            Migration = Migration(DataMigrationTrigger.AddColumn, "phone", name: "second"),
        };

        // Act
        var plan = LinearizeTable(new TableDiff("app", "users", ChangeKind.Modify, Columns: [first, second]));

        // Assert
        plan.OfType<ExecuteDataMigration>().Select(m => m.Name).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Linearize_MigrationFields_FlowThroughToTheAction()
    {
        // Arrange
        var migration = new DataMigration("dedupe", DataMigrationTrigger.AddConstraint, "app", "users", "users_pk", "DELETE FROM app.users")
        {
            RunOutsideTransaction = true,
        };
        var constraint = new PrimaryKeyDiff(ChangeKind.Add, "users_pk",
            new PrimaryKey("users_pk", ["id"])) { Migration = migration };

        // Act
        var plan = LinearizeTable(new TableDiff("app", "users", ChangeKind.Modify, PrimaryKey: [constraint]));

        // Assert
        var action = plan.OfType<ExecuteDataMigration>().ShouldHaveSingleItem();
        action.ShouldSatisfyAllConditions(
            a => a.Name.ShouldBe("dedupe"),
            a => a.Trigger.ShouldBe(DataMigrationTrigger.AddConstraint),
            a => a.SchemaName.ShouldBe("app"),
            a => a.TableName.ShouldBe("users"),
            a => a.MemberName.ShouldBe("users_pk"),
            a => a.Sql.ShouldBe("DELETE FROM app.users"),
            a => a.RunOutsideTransaction.ShouldBeTrue());
    }
}
