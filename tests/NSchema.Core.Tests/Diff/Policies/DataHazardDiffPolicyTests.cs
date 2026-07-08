using Microsoft.Extensions.Options;
using NSchema.Diagnostics;
using NSchema.Diff.Model;
using NSchema.Diff.Policies;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Constraints;
using NSchema.Schema.Model.Indexes;
using NSchema.Schema.Model.Tables;

namespace NSchema.Tests.Diff.Policies;

public class DataHazardDiffPolicyTests
{
    private readonly IOptions<DataHazardOptions> _options = Options.Create(new DataHazardOptions());

    private readonly DataHazardDiffPolicy _sut;

    public DataHazardDiffPolicyTests()
    {
        _sut = new DataHazardDiffPolicy(_options);
    }

    [Fact]
    public void Validate_RequiredColumnAddWithoutDefault_IsFlagged()
    {
        // Arrange — the founding case: ADD COLUMN NOT NULL without a DEFAULT fails against a populated table.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Source.ShouldBe("data-hazards");
        results[0].Message.ShouldContain("app.users.email");
        results[0].Message.ShouldContain("DEFAULT");
    }

    [Fact]
    public void Validate_DefaultsToWarning()
    {
        // Arrange — hazards depend on the data in the table, so the default policy warns rather than blocks.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(DiagnosticSeverity.Warning);
    }

    [Theory]
    [InlineData(DataHazardPolicy.Error, DiagnosticSeverity.Error)]
    [InlineData(DataHazardPolicy.Warn, DiagnosticSeverity.Warning)]
    [InlineData(DataHazardPolicy.Allow, DiagnosticSeverity.Info)]
    public void Validate_MapsPolicyToSeverity(DataHazardPolicy policy, DiagnosticSeverity expected)
    {
        // Arrange
        _options.Value.Policy = policy;
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Severity.ShouldBe(expected);
    }

    [Fact]
    public void Validate_RequiredColumnAddOnNewTable_IsNotFlagged()
    {
        // Arrange — an added table is empty at apply time, so nothing in it can fail on data.
        var table = new Table("users", Columns: [new Column("email", SqlType.Text)]);
        var diff = new DatabaseDiff([
            new SchemaDiff("app", Tables:
            [
                new TableDiff("app", "users", ChangeKind.Add,
                    Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))],
                    Definition: table),
            ]),
        ]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnAddWithDefault_IsNotFlagged()
    {
        // Arrange — a default gives existing rows their value, so the add cannot fail.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, DefaultExpression: "''"))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_NullableColumnAdd_IsNotFlagged()
    {
        // Arrange
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, IsNullable: true))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_IdentityColumnAdd_IsNotFlagged()
    {
        // Arrange — an identity column computes its own values for existing rows.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("id", ChangeKind.Add, new Column("id", SqlType.BigInt, IsIdentity: true))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_GeneratedColumnAdd_IsNotFlagged()
    {
        // Arrange — a generated column computes its own values for existing rows.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("total", ChangeKind.Add, new Column("total", SqlType.Int, GeneratedExpression: "a + b"))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ColumnTightenedToNotNull_IsFlagged()
    {
        // Arrange — SET NOT NULL fails if the column holds NULLs.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Modify, Nullability: new ValueChange<bool>(true, false))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("app.users.email");
        results[0].Message.ShouldContain("NOT NULL");
    }

    [Fact]
    public void Validate_ColumnLoosenedToNullable_IsNotFlagged()
    {
        // Arrange — dropping NOT NULL cannot fail on data.
        var diff = ModifiedTable(Columns:
            [new ColumnDiff("email", ChangeKind.Modify, Nullability: new ValueChange<bool>(false, true))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Theory]
    // String and binary narrowing (no length means unbounded).
    [InlineData("varchar(100)", "varchar(50)", true)]
    [InlineData("varchar(50)", "varchar(100)", false)]
    [InlineData("text", "varchar(50)", true)]
    [InlineData("varchar(50)", "text", false)]
    [InlineData("char(10)", "varchar(5)", true)]
    [InlineData("varbinary(100)", "varbinary(50)", true)]
    [InlineData("varbinary(50)", "varbinary(100)", false)]
    // Parsing text into a structured type.
    [InlineData("text", "int", true)]
    [InlineData("varchar(50)", "datetime", true)]
    [InlineData("int", "text", false)]
    // Integer narrowing.
    [InlineData("int", "smallint", true)]
    [InlineData("smallint", "bigint", false)]
    [InlineData("bigint", "int", true)]
    // Decimal capacity.
    [InlineData("decimal(10,2)", "decimal(8,2)", true)]
    [InlineData("decimal(8,2)", "decimal(10,2)", false)]
    [InlineData("decimal(10,2)", "decimal(12,4)", false)] // whole digits unchanged; scale only grows
    [InlineData("int", "decimal(8,2)", true)] // 6 whole digits cannot hold every int
    [InlineData("int", "decimal(12,2)", false)]
    [InlineData("decimal(5,0)", "int", true)]
    // Floats.
    [InlineData("double", "float", true)]
    [InlineData("float", "double", false)]
    [InlineData("double", "bigint", true)]
    [InlineData("int", "double", false)]
    // Unknown types cannot be reasoned about, so they stay silent.
    [InlineData("citext", "varchar(5)", false)]
    [InlineData("text", "citext", false)]
    public void Validate_ColumnTypeChange_FlagsCastsThatCanFail(string oldType, string newType, bool expected)
    {
        // Arrange
        var diff = ModifiedTable(Columns:
        [
            new ColumnDiff("value", ChangeKind.Modify,
                Type: new ValueChange<SqlType>(SqlType.Parse(oldType), SqlType.Parse(newType))),
        ]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.Count.ShouldBe(expected ? 1 : 0);
    }

    [Fact]
    public void Validate_PrimaryKeyAddOverExistingColumns_IsFlagged()
    {
        // Arrange — promoting existing columns to a primary key fails on duplicates or NULLs.
        var diff = ModifiedTable(PrimaryKey:
            [new PrimaryKeyDiff(ChangeKind.Add, "users_pk", new PrimaryKey("users_pk", ["tenant_id", "email"]))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("users_pk");
        results[0].Message.ShouldContain("'tenant_id', 'email'");
    }

    [Fact]
    public void Validate_UniqueConstraintAddOverExistingColumn_IsFlagged()
    {
        // Arrange
        var diff = ModifiedTable(UniqueConstraints:
            [new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"]))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("users_email_uq");
        results[0].Message.ShouldContain("column 'email'");
    }

    [Fact]
    public void Validate_UniqueConstraintOverColumnsAddedInSameDiff_IsNotFlagged()
    {
        // Arrange — a column added in the same diff starts empty, so uniqueness confined to it cannot collide.
        var diff = ModifiedTable(
            Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, IsNullable: true))],
            UniqueConstraints:
                [new UniqueConstraintDiff(ChangeKind.Add, "users_email_uq", new UniqueConstraint("users_email_uq", ["email"]))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueConstraintRemove_IsNotFlagged()
    {
        // Arrange — dropping uniqueness cannot fail on data (the destructive policy owns that concern).
        var diff = ModifiedTable(UniqueConstraints:
            [new UniqueConstraintDiff(ChangeKind.Remove, "users_email_uq")]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueIndexAddOverExistingColumn_IsFlagged()
    {
        // Arrange
        var diff = ModifiedTable(Indexes:
            [new IndexDiff(ChangeKind.Add, "ix_users_email", new TableIndex("ix_users_email", ["email"], IsUnique: true))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
        results[0].Message.ShouldContain("ix_users_email");
    }

    [Fact]
    public void Validate_UniqueIndexWithExpressionKey_IsFlagged()
    {
        // Arrange — an expression key is opaque, so it is assumed to read pre-existing data.
        var diff = ModifiedTable(Indexes:
        [
            new IndexDiff(ChangeKind.Add, "ix_users_email",
                new TableIndex("ix_users_email", [new IndexColumn("lower(email)", IsExpression: true)], IsUnique: true)),
        ]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.ShouldHaveSingleItem();
    }

    [Fact]
    public void Validate_NonUniqueIndexAdd_IsNotFlagged()
    {
        // Arrange — a plain index enforces nothing, so it cannot fail on data.
        var diff = ModifiedTable(Indexes:
            [new IndexDiff(ChangeKind.Add, "ix_users_email", new TableIndex("ix_users_email", ["email"]))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_UniqueIndexOverColumnAddedInSameDiff_IsNotFlagged()
    {
        // Arrange
        var diff = ModifiedTable(
            Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text, IsNullable: true))],
            Indexes: [new IndexDiff(ChangeKind.Add, "ix_users_email", new TableIndex("ix_users_email", ["email"], IsUnique: true))]);

        // Act / Assert
        _sut.Validate(diff).ShouldBeEmpty();
    }

    [Fact]
    public void Validate_MultipleHazards_ReturnsOneDiagnosticEach()
    {
        // Arrange
        var diff = ModifiedTable(
            Columns: [new ColumnDiff("email", ChangeKind.Add, new Column("email", SqlType.Text))],
            UniqueConstraints:
                [new UniqueConstraintDiff(ChangeKind.Add, "users_name_uq", new UniqueConstraint("users_name_uq", ["name"]))]);

        // Act
        var results = _sut.Validate(diff).ToList();

        // Assert
        results.Count.ShouldBe(2);
    }

    private static DatabaseDiff ModifiedTable(
        IReadOnlyList<ColumnDiff>? Columns = null,
        IReadOnlyList<IndexDiff>? Indexes = null,
        IReadOnlyList<PrimaryKeyDiff>? PrimaryKey = null,
        IReadOnlyList<UniqueConstraintDiff>? UniqueConstraints = null) =>
        new([
            new SchemaDiff("app", Tables:
            [
                new TableDiff("app", "users", ChangeKind.Modify,
                    Columns: Columns, Indexes: Indexes, PrimaryKey: PrimaryKey, UniqueConstraints: UniqueConstraints),
            ]),
        ]);
}
