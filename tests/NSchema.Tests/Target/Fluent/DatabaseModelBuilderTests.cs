using NSchema.Domain.Schema;
using NSchema.Target.Fluent;

namespace NSchema.Tests.Target.Fluent;

public sealed class DatabaseModelBuilderTests
{
    // ── DatabaseModelBuilder ──────────────────────────────────────────────────

    [Fact]
    public void Build_WithNoSchemas_ReturnsModelWithEmptySchemaList()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder.Build();

        // Assert
        model.Schemas.ShouldBeEmpty();
    }

    [Fact]
    public void Schema_AddsSchemaToModel()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder
            .Schema("public", _ => { })
            .Build();

        // Assert
        model.Schemas.Count.ShouldBe(1);
        model.Schemas[0].Name.ShouldBe("public");
    }

    [Fact]
    public void Schema_MultipleSchemas_AllAppearInModel()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder
            .Schema("public", _ => { })
            .Schema("admin", _ => { })
            .Build();

        // Assert
        model.Schemas.Count.ShouldBe(2);
        model.Schemas.Select(s => s.Name).ShouldBe(["public", "admin"]);
    }

    [Fact]
    public void PreDeploymentScript_AddsScriptToModel()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder
            .PreDeploymentScript("init", "SELECT 1")
            .Build();

        // Assert
        model.PreDeploymentScripts.ShouldNotBeNull();
        model.PreDeploymentScripts!.Count.ShouldBe(1);
        model.PreDeploymentScripts[0].Name.ShouldBe("init");
        model.PreDeploymentScripts[0].Sql.ShouldBe("SELECT 1");
    }

    [Fact]
    public void PostDeploymentScript_AddsScriptToModel()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder
            .PostDeploymentScript("seed", "INSERT INTO config DEFAULT VALUES")
            .Build();

        // Assert
        model.PostDeploymentScripts.ShouldNotBeNull();
        model.PostDeploymentScripts!.Count.ShouldBe(1);
        model.PostDeploymentScripts[0].Name.ShouldBe("seed");
    }

    [Fact]
    public void Build_WithNoScripts_ScriptListsAreNull()
    {
        // Arrange
        var builder = new DatabaseModelBuilder();

        // Act
        var model = builder.Build();

        // Assert
        model.PreDeploymentScripts.ShouldBeNull();
        model.PostDeploymentScripts.ShouldBeNull();
    }

    // ── SchemaBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public void SchemaBuilder_Table_AddsTableToSchema()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", _ => { });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables.Count.ShouldBe(1);
        model.Schemas[0].Tables[0].Name.ShouldBe("users");
    }

    [Fact]
    public void SchemaBuilder_MultipleTables_AllAppearInSchema()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", _ => { })
                      .Table("posts", _ => { });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables.Select(t => t.Name).ShouldBe(["users", "posts"]);
    }

    [Fact]
    public void SchemaBuilder_WasPreviouslyNamed_SetsPreviousNameOnSchema()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.WasPreviouslyNamed("old_schema");
            })
            .Build();

        // Assert
        model.Schemas[0].PreviousName.ShouldBe("old_schema");
    }

    // ── TableBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public void TableBuilder_Column_AddsColumnToTable()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Column("id", SqlType.BigInt);
                });
            })
            .Build();

        // Assert
        var table = model.Schemas[0].Tables[0];
        table.Columns.Count.ShouldBe(1);
        table.Columns[0].Name.ShouldBe("id");
        table.Columns[0].Type.ShouldBe(SqlType.BigInt);
    }

    [Fact]
    public void TableBuilder_PrimaryKey_AttachesPrimaryKeyToTable()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Column("id", SqlType.BigInt).NotNull();
                    table.PrimaryKey("pk_users", ["id"]);
                });
            })
            .Build();

        // Assert
        var table = model.Schemas[0].Tables[0];
        table.PrimaryKey.ShouldNotBeNull();
        table.PrimaryKey!.Name.ShouldBe("pk_users");
        table.PrimaryKey.ColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public void TableBuilder_ForeignKey_AddsForeignKeyToTable()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("posts", table =>
                {
                    table.Column("user_id", SqlType.BigInt);
                    table.ForeignKey("fk_posts_user", ["user_id"], "public", "users", ["id"]);
                });
            })
            .Build();

        // Assert
        var table = model.Schemas[0].Tables[0];
        table.ForeignKeys.ShouldNotBeNull();
        table.ForeignKeys!.Count.ShouldBe(1);
        table.ForeignKeys[0].Name.ShouldBe("fk_posts_user");
        table.ForeignKeys[0].ColumnNames.ShouldBe(["user_id"]);
        table.ForeignKeys[0].ReferencedSchema.ShouldBe("public");
        table.ForeignKeys[0].ReferencedTable.ShouldBe("users");
        table.ForeignKeys[0].ReferencedColumnNames.ShouldBe(["id"]);
    }

    [Fact]
    public void TableBuilder_Index_AddsIndexToTable()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Column("email", SqlType.Text);
                    table.Index("idx_users_email", ["email"]);
                });
            })
            .Build();

        // Assert
        var table = model.Schemas[0].Tables[0];
        table.Indexes.ShouldNotBeNull();
        table.Indexes!.Count.ShouldBe(1);
        table.Indexes[0].Name.ShouldBe("idx_users_email");
        table.Indexes[0].ColumnNames.ShouldBe(["email"]);
        table.Indexes[0].IsUnique.ShouldBeFalse();
    }

    [Fact]
    public void TableBuilder_WasPreviouslyNamed_SetsPreviousNameOnTable()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.WasPreviouslyNamed("members");
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].PreviousName.ShouldBe("members");
    }

    [Fact]
    public void TableBuilder_NoForeignKeys_ForeignKeysIsNull()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Column("id", SqlType.BigInt);
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys.ShouldBeNull();
    }

    [Fact]
    public void TableBuilder_NoIndexes_IndexesIsNull()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Column("id", SqlType.BigInt);
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Indexes.ShouldBeNull();
    }

    // ── ColumnBuilder ─────────────────────────────────────────────────────────

    [Fact]
    public void ColumnBuilder_Defaults_IsNullableWithNoIdentityOrDefault()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("col", SqlType.Text));
            })
            .Build();

        // Assert
        var column = model.Schemas[0].Tables[0].Columns[0];
        column.IsNullable.ShouldBeTrue();
        column.IsIdentity.ShouldBeFalse();
        column.DefaultExpression.ShouldBeNull();
        column.PreviousName.ShouldBeNull();
    }

    [Fact]
    public void ColumnBuilder_NotNull_SetsIsNullableFalse()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("col", SqlType.Text).NotNull());
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeFalse();
    }

    [Fact]
    public void ColumnBuilder_Nullable_SetsIsNullableTrue()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("col", SqlType.Text).NotNull().Nullable());
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsNullable.ShouldBeTrue();
    }

    [Fact]
    public void ColumnBuilder_Identity_SetsIsIdentityTrue()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("id", SqlType.BigInt).Identity());
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Columns[0].IsIdentity.ShouldBeTrue();
    }

    [Fact]
    public void ColumnBuilder_Default_SetsDefaultExpression()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("created_at", SqlType.DateTimeOffset).Default("now()"));
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Columns[0].DefaultExpression.ShouldBe("now()");
    }

    [Fact]
    public void ColumnBuilder_WasPreviouslyNamed_SetsPreviousName()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table => table.Column("full_name", SqlType.Text).WasPreviouslyNamed("name"));
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Columns[0].PreviousName.ShouldBe("name");
    }

    // ── ForeignKeyBuilder ─────────────────────────────────────────────────────

    [Fact]
    public void ForeignKeyBuilder_OnDelete_SetsDeleteAction()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("posts", table =>
                {
                    table.ForeignKey("fk", ["user_id"], "public", "users", ["id"])
                         .OnDelete(ReferentialAction.Cascade);
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys![0].OnDelete.ShouldBe(ReferentialAction.Cascade);
    }

    [Fact]
    public void ForeignKeyBuilder_OnUpdate_SetsUpdateAction()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("posts", table =>
                {
                    table.ForeignKey("fk", ["user_id"], "public", "users", ["id"])
                         .OnUpdate(ReferentialAction.SetNull);
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].ForeignKeys![0].OnUpdate.ShouldBe(ReferentialAction.SetNull);
    }

    [Fact]
    public void ForeignKeyBuilder_Defaults_NoActionForBothRules()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("posts", table =>
                {
                    table.ForeignKey("fk", ["user_id"], "public", "users", ["id"]);
                });
            })
            .Build();

        // Assert
        var fk = model.Schemas[0].Tables[0].ForeignKeys![0];
        fk.OnDelete.ShouldBe(ReferentialAction.NoAction);
        fk.OnUpdate.ShouldBe(ReferentialAction.NoAction);
    }

    // ── IndexBuilder ──────────────────────────────────────────────────────────

    [Fact]
    public void IndexBuilder_Unique_SetsIsUniqueTrue()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("users", table =>
                {
                    table.Index("idx_email", ["email"]).Unique();
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Indexes![0].IsUnique.ShouldBeTrue();
    }

    [Fact]
    public void IndexBuilder_CompositeIndex_PreservesColumnOrder()
    {
        // Arrange & Act
        var model = new DatabaseModelBuilder()
            .Schema("public", schema =>
            {
                schema.Table("t", table =>
                {
                    table.Index("idx_composite", ["last_name", "first_name"]);
                });
            })
            .Build();

        // Assert
        model.Schemas[0].Tables[0].Indexes![0].ColumnNames.ShouldBe(["last_name", "first_name"]);
    }
}
