using NSchema.Schema.Ddl.Model;
using NSchema.Schema.Model.Migrations;

namespace NSchema.Schema.Ddl;

internal sealed partial class DdlParser
{
    /// <summary>
    /// Parses a data-migration statement:
    /// <c>MIGRATION ['name'] FOR &lt;trigger&gt; &lt;schema&gt;.&lt;table&gt;.&lt;member&gt; [( option = value, … )] AS $$ … $$;</c>.
    /// The body is opaque SQL run only when the plan contains the matching structural change.
    /// </summary>
    private DataMigration ParseDataMigration()
    {
        Advance(); // MIGRATION
        var name = _current.Kind == TokenKind.String ? Advance().Text : null;
        ExpectKeyword("FOR");
        var trigger = ParseMigrationTrigger();
        var (schema, table, member) = ParseMemberPath();
        var runOutsideTransaction = ParseRunOutsideTransactionOptions("migration");

        if (!_current.IsKeyword("AS"))
        {
            throw Error("Expected 'AS' before the migration body.");
        }
        Advance(); // AS
        var dollar = Expect(TokenKind.DollarString, "a migration body as a dollar-quoted block ($$ … $$)");
        var body = StripDollarQuote(dollar.Text).Trim();
        Expect(TokenKind.Semicolon, "';' to end the migration");

        return new DataMigration(name, trigger, schema, table, member, body)
        {
            RunOutsideTransaction = runOutsideTransaction,
        };
    }

    private DataMigrationTrigger ParseMigrationTrigger()
    {
        if (_current.IsKeyword("ADD"))
        {
            Advance(); // ADD
            if (_current.IsKeyword("COLUMN"))
            {
                Advance();
                return DataMigrationTrigger.AddColumn;
            }
            if (_current.IsKeyword("CONSTRAINT"))
            {
                Advance();
                return DataMigrationTrigger.AddConstraint;
            }
        }
        else if (_current.IsKeyword("ALTER"))
        {
            Advance(); // ALTER
            ExpectKeyword("COLUMN");
            ExpectKeyword("TYPE");
            return DataMigrationTrigger.AlterColumnType;
        }

        throw Error("Expected 'ADD COLUMN', 'ALTER COLUMN TYPE' or 'ADD CONSTRAINT'.");
    }

    /// <summary>
    /// Parses the three-segment migration target path (<c>schema.table.member</c>, where the member is a column
    /// or constraint name depending on the trigger).
    /// </summary>
    private (string Schema, string Table, string Member) ParseMemberPath()
    {
        var schema = ExpectIdentifier("a schema name");
        Expect(TokenKind.Dot, "'.' in the migration target path");
        var table = ExpectIdentifier("a table name");
        Expect(TokenKind.Dot, "'.' in the migration target path");
        var member = ExpectIdentifier("a column or constraint name");
        return (schema, table, member);
    }
}
