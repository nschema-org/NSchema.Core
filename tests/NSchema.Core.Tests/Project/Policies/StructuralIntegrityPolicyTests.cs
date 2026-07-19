using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.CompositeTypes;
using NSchema.Model.Domains;
using NSchema.Model.Enums;
using NSchema.Model.Indexes;
using NSchema.Model.Routines;
using NSchema.Model.Schemas;
using NSchema.Model.Sequences;
using NSchema.Model.Tables;
using NSchema.Model.Views;
using NSchema.Project.Policies;

namespace NSchema.Tests.Project.Policies;

public sealed class StructuralIntegrityPolicyTests
{
    private readonly StructuralIntegrityPolicy _sut = new();

    private static Column Col(string name) => new Column { Name = new SqlIdentifier(name), Type = SqlType.BigInt };

    private static Database Db(params Table[] tables) =>
        new Database { Schemas = [new Schema { Name = new SqlIdentifier("public"), Tables = [.. tables] }] };

    [Fact]
    public void NoDiagnostics_ForAConsistentSchema()
    {
        // Arrange
        var users = new Table { Name = new SqlIdentifier("users"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("users_pk"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [Col("id")] };
        var orders = new Table
        {
            Name = new SqlIdentifier("orders"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("orders_pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), Col("user_id")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("orders_users_fk"), ColumnNames = [new SqlIdentifier("user_id")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("users"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(users, orders)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenIndexNameIsReusedAcrossTablesInASchema()
    {
        // Arrange — index names are schema-scoped, so two tables can't both declare 'ix_updated_at'.
        var invoices = new Table
        {
            Name = new SqlIdentifier("invoices"),
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("ix_updated_at"), Columns = [new IndexColumn(new SqlIdentifier("updated_at"))] }],
        };
        var orders = new Table
        {
            Name = new SqlIdentifier("orders"),
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("ix_updated_at"), Columns = [new IndexColumn(new SqlIdentifier("updated_at"))] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        var diagnostic = diagnostics.ShouldHaveSingleItem();
        diagnostic.Severity.ShouldBe(DiagnosticSeverity.Error);
        diagnostic.Message.ShouldContain("ix_updated_at");
        diagnostic.Message.ShouldContain("public.invoices");
        diagnostic.Message.ShouldContain("public.orders");
    }

    [Fact]
    public void Error_WhenAPrimaryKeyNameCollidesWithAnIndexName()
    {
        // Arrange — a PRIMARY KEY creates an index bearing its name, so it shares the schema-wide pool.
        var invoices = new Table { Name = new SqlIdentifier("invoices"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("shared_name"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [Col("id")] };
        var orders = new Table
        {
            Name = new SqlIdentifier("orders"),
            Columns = [Col("id")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("shared_name"), Columns = [new IndexColumn(new SqlIdentifier("id"))] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(invoices, orders)).ToList();

        // Assert
        diagnostics.ShouldHaveSingleItem().Message.ShouldContain("shared_name");
    }

    [Fact]
    public void NoDiagnostics_WhenTheSameIndexNameIsUsedInDifferentSchemas()
    {
        // Arrange
        Table Invoices() => new Table
        {
            Name = new SqlIdentifier("invoices"),
            Columns = [Col("updated_at")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("ix_updated_at"), Columns = [new IndexColumn(new SqlIdentifier("updated_at"))] }],
        };
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("billing"), Tables = [Invoices()] },
            new Schema { Name = new SqlIdentifier("ordering"), Tables = [Invoices()] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenTableHasNoColumns()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table { Name = new SqlIdentifier("empty") })).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("no columns"));
    }

    [Fact]
    public void Error_WhenColumnDeclaredTwice()
    {
        // Act
        var diagnostics = _sut.Validate(Db(new Table { Name = new SqlIdentifier("t"), Columns = [Col("id"), Col("id")] })).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void NoError_WhenColumnsDifferOnlyInCase()
    {
        // Identifiers are case-sensitive: "id" and "ID" are two different columns, not a duplicate.
        var diagnostics = _sut.Validate(Db(new Table { Name = new SqlIdentifier("t"), Columns = [Col("id"), Col("ID")] })).ToList();

        diagnostics.ShouldNotContain(d => d.Message.Contains("more than once"));
    }

    [Fact]
    public void Error_WhenPrimaryKeyReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table { Name = new SqlIdentifier("t"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("missing")] }, Columns = [Col("id")] };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Primary key") && d.Message.Contains("missing"));
    }

    [Fact]
    public void Error_WhenIndexReferencesUnknownColumn()
    {
        // Arrange
        var table = new Table { Name = new SqlIdentifier("t"), Columns = [Col("id")], Indexes = [new TableIndex { Name = new SqlIdentifier("ix"), Columns = ["nope"] }] };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("Index") && d.Message.Contains("nope"));
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesUnknownLocalColumn()
    {
        // Arrange
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            Columns = [Col("id")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("ghost")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("t"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("unknown local column 'ghost'"));
    }

    [Fact]
    public void Error_WhenForeignKeyArityMismatches()
    {
        // Arrange
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), Col("a"), Col("b")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("a"), new SqlIdentifier("b")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("t"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("counts must match"));
    }

    [Fact]
    public void Warning_WhenForeignKeyTargetTableIsUndeclared()
    {
        // Arrange — the target may exist unmanaged (gradual adoption), so the finding advises rather than blocks.
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            Columns = [Col("id"), Col("ref")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("ref")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("absent"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        var finding = diagnostics.ShouldHaveSingleItem();
        finding.Severity.ShouldBe(DiagnosticSeverity.Warning);
        finding.Message.ShouldContain("references table 'public.absent', which this project does not declare");
    }

    [Fact]
    public void NoError_WhenForeignKeyTargetSchemaIsUnmanaged()
    {
        // Arrange — "external" schema is not present in the document at all.
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            Columns = [Col("id"), Col("ref")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("ref")], ReferencedSchema = new SqlIdentifier("external"), ReferencedTable = new SqlIdentifier("other"), ReferencedColumnNames = [new SqlIdentifier("id")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(table)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesNonUniqueColumns()
    {
        // Arrange — target column exists but is neither a PK nor a unique index.
        var target = new Table { Name = new SqlIdentifier("target"), PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] }, Columns = [Col("id"), Col("code")] };
        var source = new Table
        {
            Name = new SqlIdentifier("source"),
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("code")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("target"), ReferencedColumnNames = [new SqlIdentifier("code")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void NoError_WhenForeignKeyReferencesUniqueIndex()
    {
        // Arrange
        var target = new Table
        {
            Name = new SqlIdentifier("target"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), Col("code")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("uq"), Columns = ["code"], IsUnique = true }],
        };
        var source = new Table
        {
            Name = new SqlIdentifier("source"),
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("code")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("target"), ReferencedColumnNames = [new SqlIdentifier("code")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenForeignKeyReferencesPredicatedUniqueIndex()
    {
        // Arrange — a partial (predicated) unique index cannot back a foreign key.
        var target = new Table
        {
            Name = new SqlIdentifier("target"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), Col("code")],
            Indexes = [new TableIndex { Name = new SqlIdentifier("uq"), Columns = ["code"], IsUnique = true, Predicate = new SqlText("code IS NOT NULL") }],
        };
        var source = new Table
        {
            Name = new SqlIdentifier("source"),
            Columns = [Col("id"), Col("code")],
            ForeignKeys = [new ForeignKey { Name = new SqlIdentifier("fk"), ColumnNames = [new SqlIdentifier("code")], ReferencedSchema = new SqlIdentifier("public"), ReferencedTable = new SqlIdentifier("target"), ReferencedColumnNames = [new SqlIdentifier("code")] }],
        };

        // Act
        var diagnostics = _sut.Validate(Db(target, source)).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("not the primary key or a unique index"));
    }

    [Fact]
    public void Error_WhenFunctionAndProcedureShareAName()
    {
        // Arrange — the parser and aggregation enforce this for parsed schemas; the policy is the catch-all
        // for JSON-sourced and code-built schemas.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"), Routines = [
                new Routine { Name = new SqlIdentifier("r"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 1 $$") },
                new Routine { Name = new SqlIdentifier("r"), RoutineKind = RoutineKind.Procedure, Arguments = new SqlText(""), Definition = new SqlText("AS $$ SELECT 1 $$") },
            ] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert
        diagnostics.ShouldContain(d => d.Message.Contains("share a single name space"));
    }

    [Fact]
    public void Error_WhenFunctionDeclaredTwice()
    {
        // Arrange
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"), Routines = [
                new Routine { Name = new SqlIdentifier("f"), RoutineKind = RoutineKind.Function, Arguments = new SqlText(""), Definition = new SqlText("RETURNS int AS $$ SELECT 1 $$") },
                new Routine { Name = new SqlIdentifier("f"), RoutineKind = RoutineKind.Function, Arguments = new SqlText("a int"), Definition = new SqlText("RETURNS int AS $$ SELECT 2 $$") },
            ] },
        ],
        };

        // Act
        var diagnostics = _sut.Validate(schema).ToList();

        // Assert — overloading is not supported: one routine per name.
        diagnostics.ShouldContain(d => d.Message.Contains("declares routine 'f' more than once"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossObjectKinds()
    {
        // A table and a view called 'foo' cannot coexist — they share one name space in the database.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"),
                Tables = [new Table { Name = new SqlIdentifier("foo"), Columns = [Col("id")] }],
                Views = [new View { Name = new SqlIdentifier("foo"), Body = new SqlText("SELECT 1") }] },
        ],
        };

        var diagnostics = _sut.Validate(schema).ToList();

        diagnostics.ShouldContain(d => d.Message.Contains("reuses the name 'foo'") && d.Message.Contains("table") && d.Message.Contains("view"));
    }

    [Fact]
    public void Error_WhenNameReusedAcrossTableAndEnum()
    {
        // Relations and types share pg_type (a relation has a row type), so a table and an enum collide too.
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"),
                Tables = [new Table { Name = new SqlIdentifier("status"), Columns = [Col("id")] }],
                Enums = [new EnumType { Name = new SqlIdentifier("status"), Values = ["a", "b"] }] },
        ],
        };

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("reuses the name 'status'"));
    }

    [Fact]
    public void Error_WhenSequenceDeclaredTwice()
    {
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"), Sequences = [new Sequence { Name = new SqlIdentifier("seq") }, new Sequence { Name = new SqlIdentifier("seq") }] },
        ],
        };

        _sut.Validate(schema).ShouldContain(d => d.Message.Contains("declares sequence 'seq' more than once"));
    }

    [Fact]
    public void NoDiagnostics_WhenNamesDifferAcrossKinds()
    {
        var schema = new Database
        {
            Schemas = [
            new Schema { Name = new SqlIdentifier("public"),
                Tables = [new Table { Name = new SqlIdentifier("t"), Columns = [Col("id")] }],
                Views = [new View { Name = new SqlIdentifier("v"), Body = new SqlText("SELECT 1") }],
                Sequences = [new Sequence { Name = new SqlIdentifier("s") }],
                CompositeTypes = [new CompositeType { Name = new SqlIdentifier("c"), Fields = [new CompositeField(new SqlIdentifier("f"), SqlType.Int)] }],
                Enums = [new EnumType { Name = new SqlIdentifier("e"), Values = ["a"] }],
                Domains = [new DomainType { Name = new SqlIdentifier("d"), DataType = SqlType.Text }] },
        ],
        };

        _sut.Validate(schema).ShouldBeEmpty();
    }

    [Fact]
    public void Error_WhenColumnHasBothDefaultAndGenerated()
    {
        var table = new Table { Name = new SqlIdentifier("t"), Columns = [new Column { Name = new SqlIdentifier("area"), Type = SqlType.Int, DefaultExpression = new SqlText("0"), GeneratedExpression = new SqlText("w * h") }] };

        _sut.Validate(Db(table)).ShouldContain(d =>
            d.Message.Contains("both a DEFAULT and a GENERATED") && d.Message.Contains("area"));
    }

    [Fact]
    public void NoDiagnostics_ForGeneratedColumnWithoutDefault()
    {
        var table = new Table
        {
            Name = new SqlIdentifier("t"),
            PrimaryKey = new PrimaryKey { Name = new SqlIdentifier("t_pk"), ColumnNames = [new SqlIdentifier("id")] },
            Columns = [Col("id"), new Column { Name = new SqlIdentifier("area"), Type = SqlType.Int, GeneratedExpression = new SqlText("w * h") }],
        };

        _sut.Validate(Db(table)).ShouldBeEmpty();
    }
}
