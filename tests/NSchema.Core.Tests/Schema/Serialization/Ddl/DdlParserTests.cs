using NSchema.Configuration;
using NSchema.Schema.Ddl;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Sequences;

namespace NSchema.Tests.Schema.Serialization.Ddl;

public sealed class DdlParserTests
{
    private static DatabaseSchema Parse(string source) => new DdlParser(source).Parse().Document.Schema;

    private static IReadOnlyList<ConfigBlock> ReadConfig(string source) => DdlReader.Instance.Read(source).Config;

    private static SchemaDefinition ParseSingleSchema(string source) => Parse(source).Schemas.ShouldHaveSingleItem();

    // -------------------------------------------------------------------------
    // Schemas
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_Empty_ProducesEmptySchema()
    {
        var schema = Parse("");
        schema.Schemas.ShouldBeEmpty();
        schema.DroppedSchemas.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_CreateSchema_ProducesSchema()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app;");
        schema.Name.ShouldBe("app");
        schema.IsPartial.ShouldBeFalse();
        schema.OldName.ShouldBeNull();
        schema.Comment.ShouldBeNull();
    }

    [Fact]
    public void Parse_CreatePartialSchema_SetsIsPartial()
        => ParseSingleSchema("CREATE PARTIAL SCHEMA app;").IsPartial.ShouldBeTrue();

    [Fact]
    public void Parse_CreateSchemaRenamedFrom_SetsOldName()
        => ParseSingleSchema("CREATE SCHEMA app RENAMED FROM legacy_app;").OldName.ShouldBe("legacy_app");

    [Fact]
    public void Parse_DocCommentBeforeSchema_BecomesComment()
        => ParseSingleSchema("--- The application schema.\nCREATE SCHEMA app;").Comment.ShouldBe("The application schema.");

    [Fact]
    public void Parse_DocBlockBeforeSchema_BecomesComment()
        => ParseSingleSchema("/** Block doc. */ CREATE SCHEMA app;").Comment.ShouldBe("Block doc.");

    [Fact]
    public void Parse_SourceCommentBeforeSchema_IsNotACatalogComment()
        => ParseSingleSchema("-- just a note\nCREATE SCHEMA app;").Comment.ShouldBeNull();

    [Fact]
    public void Parse_KeywordsAreCaseInsensitive()
        => ParseSingleSchema("create partial schema app renamed from old;").IsPartial.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // Drops
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_DropSchema_RecordsDroppedSchema()
        => Parse("DROP SCHEMA scratch;").DroppedSchemas.ShouldHaveSingleItem().ShouldBe("scratch");

    [Fact]
    public void Parse_DropTable_VivifiesSchemaAndRecordsDroppedTable()
    {
        var schema = ParseSingleSchema("DROP TABLE app.old_table;");
        schema.Name.ShouldBe("app");
        schema.DroppedTables.ShouldHaveSingleItem().ShouldBe("old_table");
    }

    [Fact]
    public void Parse_DropTableInDeclaredSchema_MergesOntoSameSchema()
    {
        var schema = ParseSingleSchema("CREATE PARTIAL SCHEMA app; DROP TABLE app.old;");
        schema.IsPartial.ShouldBeTrue();
        schema.DroppedTables.ShouldHaveSingleItem().ShouldBe("old");
    }

    // -------------------------------------------------------------------------
    // Configuration blocks (captured, not interpreted)
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_ConfigBlock_IsIgnoredByTheSchemaPath()
    {
        // The schema consumer (Parse -> DatabaseSchema) sees only the schema statements.
        var schema = Parse(
            """
            NSCHEMA (
              dialect = 'postgres'
            );
            CREATE SCHEMA app;
            """);
        schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
    }

    [Fact]
    public void Parse_UnlabelledConfigBlock_CapturesTypeAndAttributes()
    {
        var block = ReadConfig(
            """
            NSCHEMA (
              dialect = 'postgres',
              transaction_mode = 'single'
            );
            """).ShouldHaveSingleItem();

        block.Type.ShouldBe("nschema");
        block.Label.ShouldBeNull();
        block.Attribute("dialect")!.AsString().ShouldBe("postgres");
        block.Attribute("transaction_mode")!.AsString().ShouldBe("single");
    }

    [Fact]
    public void Parse_LabelledConfigBlock_CapturesLabel()
    {
        var block = ReadConfig("BACKEND file ( path = 'state/app.nsstate' );").ShouldHaveSingleItem();

        block.Type.ShouldBe("backend");
        block.Label.ShouldBe("file");
        block.Attribute("path")!.AsString().ShouldBe("state/app.nsstate");
    }

    [Fact]
    public void Parse_ConfigBlock_KeywordTypeIsLowerCased()
        => ReadConfig("Provider postgres ( x = 1 );").ShouldHaveSingleItem().Type.ShouldBe("provider");

    [Fact]
    public void Parse_ConfigBlock_CapturesValueKinds()
    {
        var block = ReadConfig(
            """
            PROVIDER postgres (
              schema_search_path = 'app',
              connection_timeout = 1000,
              statement_cache = -1,
              prefer_simple = true,
              ssl = false,
              transaction_mode = single
            );
            """).ShouldHaveSingleItem();

        block.Attribute("schema_search_path")!.Kind.ShouldBe(ConfigValueKind.String);
        block.Attribute("connection_timeout")!.AsInteger().ShouldBe(1000);
        block.Attribute("statement_cache")!.AsInteger().ShouldBe(-1);
        block.Attribute("prefer_simple")!.AsBoolean().ShouldBeTrue();
        block.Attribute("ssl")!.AsBoolean().ShouldBeFalse();
        block.Attribute("transaction_mode")!.Kind.ShouldBe(ConfigValueKind.Identifier);
    }

    [Fact]
    public void Parse_ConfigBlock_CapturesDottedKeyVerbatim()
        => ReadConfig("PROVIDER postgres ( pool.max = 10 );")
            .ShouldHaveSingleItem().Attribute("pool.max")!.AsInteger().ShouldBe(10);

    [Fact]
    public void Parse_ConfigBlock_LookupIsCaseInsensitive()
        => ReadConfig("NSCHEMA ( Dialect = 'postgres' );")
            .ShouldHaveSingleItem().Attribute("dialect")!.AsString().ShouldBe("postgres");

    [Fact]
    public void Parse_ConfigBlock_EmptyAttributeListIsAllowed()
    {
        var block = ReadConfig("NSCHEMA ();").ShouldHaveSingleItem();
        block.Type.ShouldBe("nschema");
        block.Attributes.ShouldBeEmpty();
    }

    [Fact]
    public void Parse_MultipleConfigBlocks_CapturedInDeclarationOrder()
    {
        var blocks = ReadConfig(
            """
            NSCHEMA ( dialect = 'postgres' );
            BACKEND file ( path = 'state/app.nsstate' );
            PROVIDER postgres ( schema_search_path = 'app' );
            """);

        blocks.Select(b => b.Type).ShouldBe(["nschema", "backend", "provider"]);
    }

    [Fact]
    public void Parse_ConfigAndSchema_Intermixed_BothSurface()
    {
        var document = DdlReader.Instance.Read(
            """
            NSCHEMA ( dialect = 'postgres' );
            CREATE SCHEMA app;
            PROVIDER postgres ( schema_search_path = 'app' );
            """);

        document.Schema.Schemas.ShouldHaveSingleItem().Name.ShouldBe("app");
        document.Config.Select(b => b.Type).ShouldBe(["nschema", "provider"]);
    }

    [Fact]
    public void Parse_UnknownConfigKeyword_IsCapturedNotRejected()
    {
        // Forward-compatibility: a config type the core has never heard of is still captured generically.
        var block = ReadConfig("WORKSPACE staging ( region = 'eu' );").ShouldHaveSingleItem();
        block.Type.ShouldBe("workspace");
        block.Label.ShouldBe("staging");
    }

    [Fact]
    public void Parse_ConfigBlock_DuplicateAttribute_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadConfig("NSCHEMA ( dialect = 'a', dialect = 'b' );"))
            .Message.ShouldContain("specified more than once");

    [Fact]
    public void Parse_ConfigBlock_MissingEquals_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadConfig("NSCHEMA ( dialect 'postgres' );"))
            .Message.ShouldContain("'='");

    [Fact]
    public void Parse_ConfigBlock_MissingValue_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadConfig("NSCHEMA ( dialect = );"))
            .Message.ShouldContain("configuration value");

    [Fact]
    public void Parse_ConfigBlock_Unterminated_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadConfig("BACKEND file ( path = 'x'"));

    [Fact]
    public void Parse_ConfigBlock_MissingSemicolon_Throws()
        => Should.Throw<DdlSyntaxException>(() => ReadConfig("NSCHEMA ( dialect = 'postgres' )"))
            .Message.ShouldContain("';'");

    // -------------------------------------------------------------------------
    // Multiple statements
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_MultipleStatements_AccumulateInOrder()
    {
        var schema = Parse(
            """
            CREATE SCHEMA app;
            CREATE SCHEMA reporting;
            DROP SCHEMA scratch;
            """);
        schema.Schemas.Select(s => s.Name).ShouldBe(["app", "reporting"]);
        schema.DroppedSchemas.ShouldHaveSingleItem().ShouldBe("scratch");
    }

    // -------------------------------------------------------------------------
    // Errors
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_MissingSemicolon_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app")).Message.ShouldContain("';'");

    [Fact]
    public void Parse_DuplicateSchema_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE SCHEMA app;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_UnknownAfterCreate_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE THING app;")).Message.ShouldContain("Expected SCHEMA, TABLE, VIEW, MATERIALIZED VIEW, ENUM, DOMAIN, TYPE, SEQUENCE, FUNCTION, PROCEDURE, EXTENSION, TRIGGER or INDEX");

    [Fact]
    public void Parse_PartialTable_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE PARTIAL TABLE app.t (id int);")).Message.ShouldContain("PARTIAL applies to SCHEMA");

    [Fact]
    public void Parse_EmptyTableBody_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE TABLE app.users ();")).Message.ShouldContain("column or constraint");

    // -------------------------------------------------------------------------
    // Standalone indexes (CREATE INDEX ... ON s.t) — equivalent to an inline table index
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_StandaloneIndexOnTable_AttachesToTable()
    {
        var table = ParseSingleSchema(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int NOT NULL, email text NOT NULL); " +
            "CREATE UNIQUE INDEX users_email_ix ON app.users (email) WHERE (email IS NOT NULL);")
            .Tables.ShouldHaveSingleItem();
        var index = table.Indexes.ShouldHaveSingleItem();
        index.Name.ShouldBe("users_email_ix");
        index.Columns.Select(c => c.Expression).ShouldBe(["email"]);
        index.IsUnique.ShouldBeTrue();
        index.Predicate.ShouldBe("email IS NOT NULL");
    }

    [Fact]
    public void Parse_StandaloneAndInlineIndexes_Coexist()
    {
        var table = ParseSingleSchema(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int NOT NULL, email text NOT NULL, INDEX users_id_ix (id)); " +
            "CREATE INDEX users_email_ix ON app.users (email);")
            .Tables.ShouldHaveSingleItem();
        table.Indexes.Select(i => i.Name).ShouldBe(["users_id_ix", "users_email_ix"], ignoreOrder: true);
    }

    [Fact]
    public void Parse_StandaloneIndexBeforeItsTable_StillAttaches()
        // Build-time resolution: the index may be declared before the CREATE TABLE it targets.
        => ParseSingleSchema("CREATE INDEX users_id_ix ON app.users (id); CREATE TABLE app.users (id int NOT NULL);")
            .Tables.ShouldHaveSingleItem().Indexes.ShouldHaveSingleItem().Name.ShouldBe("users_id_ix");

    [Fact]
    public void Parse_StandaloneIndexDuplicatingInlineName_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse(
            "CREATE SCHEMA app; CREATE TABLE app.users (id int NOT NULL, INDEX dup (id)); CREATE INDEX dup ON app.users (id);"))
            .Message.ShouldContain("already declared");

    // -------------------------------------------------------------------------
    // Views
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_CreateView_CapturesBodyVerbatim()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.active AS SELECT id, name FROM app.users WHERE active;");
        var view = schema.Views.ShouldHaveSingleItem();
        view.Name.ShouldBe("active");
        view.Body.ShouldBe("SELECT id, name FROM app.users WHERE active");
    }

    [Fact]
    public void Parse_CreateView_ExtractsDependencies()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.report AS SELECT * FROM app.orders o JOIN app.customers c ON o.cid = c.id;");
        var view = schema.Views.ShouldHaveSingleItem();
        view.DependsOn.Select(d => $"{d.Schema}.{d.Name}").ShouldBe(["app.orders", "app.customers"]);
    }

    [Fact]
    public void Parse_CreateView_RenamedFrom_SetsOldName()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.v RENAMED FROM old_v AS SELECT 1 FROM app.t;");
        schema.Views.ShouldHaveSingleItem().OldName.ShouldBe("old_v");
    }

    [Fact]
    public void Parse_CreateView_WithDocComment_AttachesComment()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app;\n--- active users\nCREATE VIEW app.active AS SELECT * FROM app.users;");
        schema.Views.ShouldHaveSingleItem().Comment.ShouldBe("active users");
    }

    [Fact]
    public void Parse_DropView_RecordsDroppedView()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; DROP VIEW app.stale;");
        schema.DroppedViews.ShouldHaveSingleItem().ShouldBe("stale");
    }

    [Fact]
    public void Parse_DuplicateView_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1 FROM app.t; CREATE VIEW app.v AS SELECT 2 FROM app.t;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_PartialView_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE PARTIAL VIEW app.v AS SELECT 1 FROM app.t;")).Message.ShouldContain("PARTIAL applies to SCHEMA");

    [Fact]
    public void Parse_ViewBodyWithSemicolonInString_StopsAtRealTerminator()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT ';' AS marker FROM app.t;");
        schema.Views.ShouldHaveSingleItem().Body.ShouldBe("SELECT ';' AS marker FROM app.t");
    }

    [Fact]
    public void Parse_ViewBodyWithSemicolonInComment_StopsAtRealTerminator_AndKeepsTheComment()
    {
        // A ';' inside a line comment is not a terminator (the lexer skips the comment, so no ';' token is produced),
        // and the comment text survives verbatim because the body is recovered by slicing the source.
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1 -- a; b\nFROM app.t;");
        schema.Views.ShouldHaveSingleItem().Body.ShouldBe("SELECT 1 -- a; b\nFROM app.t");
    }

    [Fact]
    public void Parse_ViewBodyWithSemicolonInBlockComment_StopsAtRealTerminator()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE VIEW app.v AS SELECT 1 /* a; b */ FROM app.t;")
            .Views.ShouldHaveSingleItem().Body.ShouldBe("SELECT 1 /* a; b */ FROM app.t");

    // -------------------------------------------------------------------------
    // Enums
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_CreateEnum_CapturesOrderedValues()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE ENUM app.status ('pending', 'shipped', 'delivered');");
        var enumType = schema.Enums.ShouldHaveSingleItem();
        enumType.Name.ShouldBe("status");
        enumType.Values.ShouldBe(["pending", "shipped", "delivered"]);
    }

    [Fact]
    public void Parse_CreateEnum_WithEmptyValueList_IsAllowed()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE ENUM app.status ();")
            .Enums.ShouldHaveSingleItem().Values.ShouldBeEmpty();

    [Fact]
    public void Parse_CreateEnum_UnescapesQuotedQuote()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE ENUM app.status ('it''s');")
            .Enums.ShouldHaveSingleItem().Values.ShouldBe(["it's"]);

    [Fact]
    public void Parse_CreateEnum_RenamedFrom_SetsOldName()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE ENUM app.status RENAMED FROM state ('a');")
            .Enums.ShouldHaveSingleItem().OldName.ShouldBe("state");

    [Fact]
    public void Parse_CreateEnum_WithDocComment_AttachesComment()
        => ParseSingleSchema("CREATE SCHEMA app;\n--- order lifecycle\nCREATE ENUM app.status ('a');")
            .Enums.ShouldHaveSingleItem().Comment.ShouldBe("order lifecycle");

    [Fact]
    public void Parse_DropEnum_RecordsDroppedEnum()
        => ParseSingleSchema("CREATE SCHEMA app; DROP ENUM app.stale;")
            .DroppedEnums.ShouldHaveSingleItem().ShouldBe("stale");

    [Fact]
    public void Parse_DuplicateEnum_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE ENUM app.e ('a'); CREATE ENUM app.e ('b');"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_DuplicateEnumValue_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE ENUM app.e ('a', 'a');"))
            .Message.ShouldContain("more than once");

    [Fact]
    public void Parse_EnumValueMustBeQuoted_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE ENUM app.e (pending);"))
            .Message.ShouldContain("an enum value");

    [Fact]
    public void Parse_PartialEnum_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE PARTIAL ENUM app.e ('a');"))
            .Message.ShouldContain("PARTIAL applies to SCHEMA");

    // -------------------------------------------------------------------------
    // Sequences
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_CreateSequence_WithoutOptions_HasEmptyOptions()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE SEQUENCE app.order_id;");
        var sequence = schema.Sequences.ShouldHaveSingleItem();
        sequence.Name.ShouldBe("order_id");
        sequence.Options.ShouldBe(new SequenceOptions());
    }

    [Fact]
    public void Parse_CreateSequence_WithEveryOption_CapturesThemAll()
    {
        var schema = ParseSingleSchema(
            "CREATE SCHEMA app; CREATE SEQUENCE app.order_id (AS bigint, START 100, INCREMENT 5, MINVALUE 1, MAXVALUE 999999, CACHE 10, CYCLE);");
        schema.Sequences.ShouldHaveSingleItem().Options.ShouldBe(
            new SequenceOptions(SqlType.BigInt, StartWith: 100, IncrementBy: 5, MinValue: 1, MaxValue: 999999, Cache: 10, Cycle: true));
    }

    [Fact]
    public void Parse_CreateSequence_WithNegativeValues_ParsesSign()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE SEQUENCE app.countdown (START -1, INCREMENT -1, MINVALUE -100);");
        schema.Sequences.ShouldHaveSingleItem().Options.ShouldBe(
            new SequenceOptions(StartWith: -1, IncrementBy: -1, MinValue: -100));
    }

    [Fact]
    public void Parse_CreateSequence_RenamedFrom_SetsOldName()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE SEQUENCE app.invoice_id RENAMED FROM bill_id;")
            .Sequences.ShouldHaveSingleItem().OldName.ShouldBe("bill_id");

    [Fact]
    public void Parse_CreateSequence_WithDocComment_AttachesComment()
        => ParseSingleSchema("CREATE SCHEMA app;\n--- order numbers\nCREATE SEQUENCE app.order_id;")
            .Sequences.ShouldHaveSingleItem().Comment.ShouldBe("order numbers");

    [Fact]
    public void Parse_DropSequence_RecordsDroppedSequence()
        => ParseSingleSchema("CREATE SCHEMA app; DROP SEQUENCE app.stale;")
            .DroppedSequences.ShouldHaveSingleItem().ShouldBe("stale");

    [Fact]
    public void Parse_DuplicateSequence_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE SEQUENCE app.q; CREATE SEQUENCE app.q;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_UnknownSequenceOption_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE SEQUENCE app.q (WIBBLE 1);"))
            .Message.ShouldContain("Unknown sequence option 'WIBBLE'");

    [Fact]
    public void Parse_DuplicateSequenceOption_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE SEQUENCE app.q (START 1, START 2);"))
            .Message.ShouldContain("more than once");

    [Fact]
    public void Parse_PartialSequence_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE PARTIAL SEQUENCE app.q;"))
            .Message.ShouldContain("PARTIAL applies to SCHEMA");

    // -------------------------------------------------------------------------
    // Functions and procedures
    // -------------------------------------------------------------------------

    [Fact]
    public void Parse_CreateFunction_CapturesArgumentsAndDefinitionVerbatim()
    {
        var schema = ParseSingleSchema(
            "CREATE SCHEMA app; CREATE FUNCTION app.add_tax(amount numeric, rate numeric) RETURNS numeric LANGUAGE sql AS $$ SELECT amount * (1 + rate); $$;");
        var function = schema.Routines.ShouldHaveSingleItem();
        function.Name.ShouldBe("add_tax");
        function.Arguments.ShouldBe("amount numeric, rate numeric");
        function.Definition.ShouldBe("RETURNS numeric LANGUAGE sql AS $$ SELECT amount * (1 + rate); $$");
    }

    [Fact]
    public void Parse_CreateFunction_DollarQuotedBodyWithInternalSemicolons_RunsToTheRealTerminator()
    {
        var schema = ParseSingleSchema(
            "CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int LANGUAGE plpgsql AS $body$ BEGIN RETURN 1; END; $body$; CREATE TABLE app.t (id int);");
        schema.Routines.ShouldHaveSingleItem().Definition.ShouldContain("BEGIN RETURN 1; END;");
        schema.Tables.ShouldHaveSingleItem(); // parsing resumed correctly after the function
    }

    [Fact]
    public void Parse_CreateFunction_ArgumentsWithQuotedDefault_AreCapturedVerbatim()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE FUNCTION app.f(code text DEFAULT 'a;b)') RETURNS int AS $$ SELECT 1 $$;")
            .Routines.ShouldHaveSingleItem().Arguments.ShouldBe("code text DEFAULT 'a;b)'");

    [Fact]
    public void Parse_CreateFunction_EmptyArguments_AreEmptyString()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$;")
            .Routines.ShouldHaveSingleItem().Arguments.ShouldBe("");

    [Fact]
    public void Parse_CreateFunction_MissingDefinition_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE SCHEMA app; CREATE FUNCTION app.f();"))
            .Message.ShouldContain("Expected a function definition");

    [Fact]
    public void Parse_CreateFunction_RenamedFrom_SetsOldName()
        => ParseSingleSchema("CREATE SCHEMA app; CREATE FUNCTION app.f RENAMED FROM old_f() RETURNS int AS $$ SELECT 1 $$;")
            .Routines.ShouldHaveSingleItem().OldName.ShouldBe("old_f");

    [Fact]
    public void Parse_CreateFunction_WithDocComment_AttachesComment()
        => ParseSingleSchema("CREATE SCHEMA app;\n--- adds tax\nCREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$;")
            .Routines.ShouldHaveSingleItem().Comment.ShouldBe("adds tax");

    [Fact]
    public void Parse_PartialFunction_Throws()
        => Should.Throw<DdlSyntaxException>(() => Parse("CREATE PARTIAL FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$;"))
            .Message.ShouldContain("PARTIAL applies to SCHEMA");

    [Fact]
    public void Parse_CreateProcedure_ParsesWithoutReturns()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; CREATE PROCEDURE app.archive(before date) LANGUAGE sql AS $$ DELETE FROM app.t; $$;");
        var procedure = schema.Routines.ShouldHaveSingleItem();
        procedure.Name.ShouldBe("archive");
        procedure.Arguments.ShouldBe("before date");
        procedure.Definition.ShouldBe("LANGUAGE sql AS $$ DELETE FROM app.t; $$");
    }

    [Fact]
    public void Parse_DuplicateFunction_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 1 $$; CREATE FUNCTION app.f() RETURNS int AS $$ SELECT 2 $$;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_DuplicateProcedure_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE PROCEDURE app.p() AS $$ SELECT 1 $$; CREATE PROCEDURE app.p() AS $$ SELECT 2 $$;"))
            .Message.ShouldContain("already declared");

    [Fact]
    public void Parse_ProcedureNamedLikeAFunction_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE FUNCTION app.r() RETURNS int AS $$ SELECT 1 $$; CREATE PROCEDURE app.r() AS $$ SELECT 1 $$;"))
            .Message.ShouldContain("share one name space");

    [Fact]
    public void Parse_FunctionNamedLikeAProcedure_Throws()
        => Should.Throw<DdlSyntaxException>(() =>
            Parse("CREATE SCHEMA app; CREATE PROCEDURE app.r() AS $$ SELECT 1 $$; CREATE FUNCTION app.r() RETURNS int AS $$ SELECT 1 $$;"))
            .Message.ShouldContain("share one name space");

    [Fact]
    public void Parse_DropFunctionAndProcedure_RecordDrops()
    {
        var schema = ParseSingleSchema("CREATE SCHEMA app; DROP FUNCTION app.stale_fn; DROP PROCEDURE app.stale_proc;");
        schema.DroppedRoutines.ShouldBe(["stale_fn", "stale_proc"], ignoreOrder: true);
    }
}
