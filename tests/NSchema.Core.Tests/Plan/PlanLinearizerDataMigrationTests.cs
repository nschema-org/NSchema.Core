using NSchema.Diff.Model;
using NSchema.Diff.Model.Columns;
using NSchema.Diff.Model.Constraints;
using NSchema.Diff.Model.Schemas;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Plan.Model;
using NSchema.Plan.Model.Columns;
using NSchema.Plan.Model.Constraints;
using NSchema.Plan.Model.Scripts;
using NSchema.Plan.Model.Services;

namespace NSchema.Tests.Plan;

/// <summary>
/// Exercises <see cref="PlanLinearizer"/>'s handling of diff nodes annotated with a matched change-event
/// script: the backfill decomposition of a required column add, the ordering of
/// <see cref="ExecuteScript"/> around type changes and constraint adds, and the script flow-through.
/// </summary>
public sealed class PlanLinearizerDataMigrationTests
{
    private readonly PlanLinearizer _linearizer = new();

    private IReadOnlyList<MigrationAction> LinearizeTable(TableDiff table)
        => _linearizer.Linearize(new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [table])]));

    private IReadOnlyList<MigrationAction> LinearizeColumn(ColumnDiff column)
        => LinearizeTable(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [column]));

    private static ChangeScript Migration(ChangeTrigger trigger, string member, string? name = null, string? sql = null) =>
        new(new SqlIdentifier(name ?? member), new SqlText(sql ?? $"UPDATE app.users -- {member}"), new SqlIdentifier("app"), trigger, new SqlIdentifier("users"), new SqlIdentifier(member));

    [Fact]
    public void Linearize_AnnotatedRequiredColumnAdd_DecomposesIntoNullableAddBackfillAndTighten()
    {
        // Arrange — a NOT NULL, no-default column with a matched backfill cannot land in one step against a
        // populated table: it is added nullable, backfilled, then tightened.
        var migration = Migration(ChangeTrigger.AddColumn, "email", name: "backfill_emails");
        var column = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(3);
        var add = plan[0].ShouldBeOfType<AddColumn>();
        add.Column.Name.ShouldBe("email");
        add.Column.IsNullable.ShouldBeTrue();
        var backfill = plan[1].ShouldBeOfType<ExecuteScript>();
        backfill.Script.Name.ShouldBe("backfill_emails");
        backfill.Script.Sql.ShouldBe(migration.Sql);
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
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var column = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text, isNullable: true)) { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.IsNullable.ShouldBeTrue();
        plan[1].ShouldBeOfType<ExecuteScript>();
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
            "defaulted" => new Column(new SqlIdentifier("email"), SqlType.Text, defaultExpression: new SqlText("''")),
            "identity" => new Column(new SqlIdentifier("email"), SqlType.BigInt, isIdentity: true),
            _ => new Column(new SqlIdentifier("email"), SqlType.Text, generatedExpression: new SqlText("lower(name)")),
        };
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var column = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, definition) { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<AddColumn>().Column.ShouldBe(definition);
        plan[1].ShouldBeOfType<ExecuteScript>();
        plan.OfType<AlterColumnNullability>().ShouldBeEmpty();
    }

    [Fact]
    public void Linearize_AnnotatedTypeChange_RunsMigrationBeforeAlterColumnType()
    {
        // Arrange — the migration's SQL prepares the data for the cast, so it must run first.
        var migration = Migration(ChangeTrigger.AlterColumnType, "total");
        var column = new ColumnDiff(new SqlIdentifier("total"), ChangeKind.Modify,
            Type: new ValueChange<SqlType>(SqlType.Text, SqlType.Int))
        { MigrationScript = migration };

        // Act
        var plan = LinearizeColumn(column);

        // Assert
        plan.Count.ShouldBe(2);
        var prep = plan[0].ShouldBeOfType<ExecuteScript>();
        prep.Script.ShouldBe(migration);
        var alter = plan[1].ShouldBeOfType<AlterColumnType>();
        alter.OldType.ShouldBe(SqlType.Text);
        alter.NewType.ShouldBe(SqlType.Int);
    }

    [Fact]
    public void Linearize_AnnotatedUniqueConstraintAdd_RunsMigrationBeforeAddUniqueConstraint()
    {
        // Arrange — the migration de-duplicates the data the constraint depends on.
        var migration = Migration(ChangeTrigger.AddConstraint, "users_email_uq");
        var constraint = new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"),
            new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]))
        { MigrationScript = migration };

        // Act
        var plan = LinearizeTable(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, UniqueConstraints: [constraint]));

        // Assert
        plan.Count.ShouldBe(2);
        plan[0].ShouldBeOfType<ExecuteScript>().Script.ShouldBe(migration);
        plan[1].ShouldBeOfType<AddUniqueConstraint>().UniqueConstraint.Name.ShouldBe("users_email_uq");
    }

    [Fact]
    public void Linearize_TwoAnnotatedChanges_KeepMigrationsInDiffOrder()
    {
        // Arrange — two annotated column adds; their migrations share a priority band, so the stable sort
        // preserves the diff's declaration order.
        var first = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text, isNullable: true))
        {
            MigrationScript = Migration(ChangeTrigger.AddColumn, "email", name: "first"),
        };
        var second = new ColumnDiff(new SqlIdentifier("phone"), ChangeKind.Add, new Column(new SqlIdentifier("phone"), SqlType.Text, isNullable: true))
        {
            MigrationScript = Migration(ChangeTrigger.AddColumn, "phone", name: "second"),
        };

        // Act
        var plan = LinearizeTable(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [first, second]));

        // Assert
        plan.OfType<ExecuteScript>().Select(m => m.Script.Name).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Linearize_MatchedScript_RidesTheActionWhole()
    {
        // Arrange
        var migration = new ChangeScript(new SqlIdentifier("dedupe"), new SqlText("DELETE FROM app.users"),
            new SqlIdentifier("app"), ChangeTrigger.AddConstraint, new SqlIdentifier("users"), new SqlIdentifier("users_pk"))
        {
            RunOutsideTransaction = true,
        };
        var constraint = new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pk"),
            new PrimaryKey(new SqlIdentifier("users_pk"), [new SqlIdentifier("id")]))
        { MigrationScript = migration };

        // Act
        var plan = LinearizeTable(new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, PrimaryKey: [constraint]));

        // Assert — the action carries the declared script itself, nothing copied field-by-field.
        plan.OfType<ExecuteScript>().ShouldHaveSingleItem().Script.ShouldBe(migration);
    }
}
