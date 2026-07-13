using NSchema.Diff.Domain;
using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Constraints;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Columns;
using NSchema.Project.Domain.Models.Constraints;
using NSchema.Project.Domain.Models.Scripts;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Tests.Diff;

public class MigrationAnnotatorTests
{
    [Fact]
    public void Apply_AddColumnTrigger_AnnotatesAddedColumn()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AlterColumnTypeTrigger_AnnotatesTypeChangedColumn()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AlterColumnType, "total");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("total"), ChangeKind.Modify, Type: new ValueChange<SqlType>(SqlType.Text, SqlType.Int))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AddConstraintTrigger_AnnotatesAddedPrimaryKey()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddConstraint, "users_pk");
        var diff = ModifiedTable(PrimaryKey:
            [new PrimaryKeyDiff(ChangeKind.Add, new SqlIdentifier("users_pk"), new PrimaryKey(new SqlIdentifier("users_pk"), [new SqlIdentifier("id")]))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].PrimaryKey[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AddConstraintTrigger_AnnotatesAddedUniqueConstraint()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddConstraint, "users_email_uq");
        var diff = ModifiedTable(UniqueConstraints:
            [new UniqueConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_email_uq"), new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")]))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].UniqueConstraints[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AddConstraintTrigger_AnnotatesAddedForeignKey()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddConstraint, "users_org_fk");
        var diff = ModifiedTable(ForeignKeys:
            [new ForeignKeyDiff(ChangeKind.Add, new SqlIdentifier("users_org_fk"), new ForeignKey(new SqlIdentifier("users_org_fk"), [new SqlIdentifier("org_id")], new SqlIdentifier("app"), new SqlIdentifier("orgs"), [new SqlIdentifier("id")]))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].ForeignKeys[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AddConstraintTrigger_AnnotatesAddedCheckConstraint()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddConstraint, "users_age_chk");
        var diff = ModifiedTable(Checks:
            [new CheckConstraintDiff(ChangeKind.Add, new SqlIdentifier("users_age_chk"), new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0")))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Checks[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_AddConstraintTrigger_AnnotatesAddedExclusionConstraint()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddConstraint, "no_overlap");
        var diff = ModifiedTable(ExclusionConstraints:
        [
            new ExclusionConstraintDiff(ChangeKind.Add, new SqlIdentifier("no_overlap"),
                new ExclusionConstraint(new SqlIdentifier("no_overlap"), [new ExclusionElement("&&", new SqlIdentifier("during"))], "gist")),
        ]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].ExclusionConstraints[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_WrongMemberName_DoesNotMatch()
    {
        // Arrange
        var migration = Migration(ChangeTrigger.AddColumn, "phone");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        unmatched.ShouldBe([migration]);
    }

    [Fact]
    public void Apply_WrongTrigger_DoesNotMatch()
    {
        // Arrange — an AlterColumnType block targeting a column that is being added, not retyped.
        var migration = Migration(ChangeTrigger.AlterColumnType, "email");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        unmatched.ShouldBe([migration]);
    }

    [Fact]
    public void Apply_AlterColumnTypeTrigger_DoesNotMatchModifyWithoutTypeChange()
    {
        // Arrange — the column changes, but not its type, so a type-change migration has nothing to prepare.
        var migration = Migration(ChangeTrigger.AlterColumnType, "email");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Modify, Nullability: new ValueChange<bool>(true, false))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        unmatched.ShouldBe([migration]);
    }

    [Fact]
    public void Apply_BlockTargetingAddedTable_LandsInUnmatched()
    {
        // Arrange — an added table is empty, so a migration targeting it has no data to move.
        var migration = Migration(ChangeTrigger.AddColumn, "email");
        var diff = new DatabaseDiff([
            new SchemaDiff(new SqlIdentifier("app"), Tables:
            [
                new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Add,
                    Columns: [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))],
                    Definition: new Table(new SqlIdentifier("users"), Columns: [new Column(new SqlIdentifier("email"), SqlType.Text)])),
            ]),
        ]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBeNull();
        unmatched.ShouldBe([migration]);
    }

    [Fact]
    public void Apply_MatchesCaseInsensitively_OnSchemaTableAndMember()
    {
        // Arrange
        var migration = new Script(new SqlIdentifier("backfill"), new SqlText("UPDATE 1"), new ChangeEvent(ChangeTrigger.AddColumn, new SqlIdentifier("Users"), new SqlIdentifier("EMAIL")) { ScopeSchema = new SqlIdentifier("APP") });
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [migration]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(migration.Name);
        unmatched.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_MixedBlocks_OnlyUnmatchedSurvive_InDeclarationOrder()
    {
        // Arrange — two dead blocks straddle a matched one; the matched block disappears and the rest keep order.
        var deadFirst = Migration(ChangeTrigger.AddColumn, "phone", name: "first");
        var matched = Migration(ChangeTrigger.AddColumn, "email", name: "matched");
        var deadLast = Migration(ChangeTrigger.AddConstraint, "missing_uq", name: "last");
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, [deadFirst, matched, deadLast]);

        // Assert
        annotated.Schemas[0].Tables[0].Columns[0].MigrationScript.ShouldBe(matched.Name);
        unmatched.ShouldBe([deadFirst, deadLast]);
    }

    [Fact]
    public void Apply_NoMigrations_ReturnsTheSameDiffInstance()
    {
        // Arrange
        var diff = ModifiedTable(Columns:
            [new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text))]);

        // Act
        var (annotated, unmatched) = MigrationAnnotator.Annotate(diff, []);

        // Assert — nothing to match means nothing to rewrite: the exact input instance comes back.
        annotated.ShouldBeSameAs(diff);
        unmatched.ShouldBeEmpty();
    }

    private static Script Migration(ChangeTrigger trigger, string member, string? name = null) =>
        new(new SqlIdentifier(name ?? member), new SqlText($"UPDATE app.users -- {member}"), new ChangeEvent(trigger, new SqlIdentifier("users"), new SqlIdentifier(member)) { ScopeSchema = new SqlIdentifier("app") });

    private static DatabaseDiff ModifiedTable(
        IReadOnlyList<ColumnDiff>? Columns = null,
        IReadOnlyList<PrimaryKeyDiff>? PrimaryKey = null,
        IReadOnlyList<ForeignKeyDiff>? ForeignKeys = null,
        IReadOnlyList<UniqueConstraintDiff>? UniqueConstraints = null,
        IReadOnlyList<CheckConstraintDiff>? Checks = null,
        IReadOnlyList<ExclusionConstraintDiff>? ExclusionConstraints = null) =>
        new([
            new SchemaDiff(new SqlIdentifier("app"), Tables:
            [
                new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify,
                    Columns: Columns, PrimaryKey: PrimaryKey, ForeignKeys: ForeignKeys,
                    UniqueConstraints: UniqueConstraints, Checks: Checks, ExclusionConstraints: ExclusionConstraints),
            ]),
        ]);
}
