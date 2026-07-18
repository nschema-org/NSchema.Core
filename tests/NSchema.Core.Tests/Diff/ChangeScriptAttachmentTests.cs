using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model.Services;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using DatabaseComparer = NSchema.Diff.Model.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// The change-event script attachment: a change script rides the diff node it accompanies, attached by the
/// decorator pass over the computed diff.
/// </summary>
public class ChangeScriptAttachmentTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance);

    /// <summary>Diffs the given current/desired <c>app.users</c> tables, steered by the given change scripts.</summary>
    private TableDiff Diff(Table current, Table desired, params ChangeScript[] scripts)
    {
        var currentDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [current] }] };
        var desiredDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [desired] }] };
        var diff = _sut.Compare(AlignedDatabase.Unaligned(currentDb), desiredDb);
        return ChangeScriptDecorator.Decorate(diff, scripts).Require().Schemas.Single().Tables.Single();
    }

    private static Table Users(params object[] members)
    {
        return new Table
        {
            Name = new SqlIdentifier("users"),
            Columns = [.. members.OfType<Column>()],
            PrimaryKey = members.OfType<PrimaryKey>().FirstOrDefault(),
            ForeignKeys = [.. members.OfType<ForeignKey>()],
            UniqueConstraints = [.. members.OfType<UniqueConstraint>()],
            CheckConstraints = [.. members.OfType<CheckConstraint>()],
            ExclusionConstraints = [.. members.OfType<ExclusionConstraint>()],
        };
    }

    private static Column Id => new Column { Name = new SqlIdentifier("id"), Type = SqlType.Int };

    private static ChangeScript Change(ChangeTrigger trigger, string member, string? name = null) =>
        new(new SqlIdentifier(name ?? member), new SqlText($"UPDATE app.users -- {member}"), new SqlIdentifier("app"),
            trigger, new SqlIdentifier("users"), new SqlIdentifier(member));

    [Fact]
    public void AddColumn_AttachesToTheAddedColumn()
    {
        var script = Change(ChangeTrigger.AddColumn, "email");
        var diff = Diff(Users(Id), Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AlterColumnType_AttachesToTheRetypedColumn()
    {
        var script = Change(ChangeTrigger.AlterColumnType, "total");
        var diff = Diff(
            Users(Id, new Column { Name = new SqlIdentifier("total"), Type = SqlType.Text }),
            Users(Id, new Column { Name = new SqlIdentifier("total"), Type = SqlType.Int }),
            script);

        diff.Columns.Single(c => c.Name.Value == "total").MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedPrimaryKey()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_pk");
        var diff = Diff(
            Users(Id),
            Users(Id, new PrimaryKey { Name = new SqlIdentifier("users_pk"), ColumnNames = [new SqlIdentifier("id")] }),
            script);

        diff.PrimaryKey.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedUniqueConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_email_uq");
        Column Email() => new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text };
        var diff = Diff(
            Users(Id, Email()),
            Users(Id, Email(), new UniqueConstraint { Name = new SqlIdentifier("users_email_uq"), ColumnNames = [new SqlIdentifier("email")] }),
            script);

        diff.UniqueConstraints.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedForeignKey()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_org_fk");
        Column OrgId() => new Column { Name = new SqlIdentifier("org_id"), Type = SqlType.Int };
        var diff = Diff(
            Users(Id, OrgId()),
            Users(Id, OrgId(), new ForeignKey { Name = new SqlIdentifier("users_org_fk"), ColumnNames = [new SqlIdentifier("org_id")], ReferencedSchema = new SqlIdentifier("app"), ReferencedTable = new SqlIdentifier("orgs"), ReferencedColumnNames = [new SqlIdentifier("id")] }),
            script);

        diff.ForeignKeys.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedCheckConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_age_chk");
        Column Age() => new Column { Name = new SqlIdentifier("age"), Type = SqlType.Int };
        var diff = Diff(
            Users(Id, Age()),
            Users(Id, Age(), new CheckConstraint { Name = new SqlIdentifier("users_age_chk"), Expression = new SqlText("age >= 0") }),
            script);

        diff.Checks.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedExclusionConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "no_overlap");
        Column During() => new Column { Name = new SqlIdentifier("during"), Type = SqlType.Text };
        var diff = Diff(
            Users(Id, During()),
            Users(Id, During(), new ExclusionConstraint { Name = new SqlIdentifier("no_overlap"), Elements = [new ExclusionElement("&&", new SqlIdentifier("during"))], Method = "gist" }),
            script);

        diff.ExclusionConstraints.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void WrongMemberName_DoesNotAttach()
    {
        var script = Change(ChangeTrigger.AddColumn, "phone");
        var diff = Diff(Users(Id), Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void WrongTrigger_DoesNotAttach()
    {
        // An AlterColumnType script targeting a column that is being added, not retyped.
        var script = Change(ChangeTrigger.AlterColumnType, "email");
        var diff = Diff(Users(Id), Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void UnattachedScript_ReportsADeadMigrationDiagnostic()
    {
        // Nothing in the diff matches the script, so decoration leaves it behind and says so.
        var script = Change(ChangeTrigger.AddColumn, "phone");
        var currentDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [Users(Id)] }] };
        var desiredDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text })] }] };
        var diff = _sut.Compare(AlignedDatabase.Unaligned(currentDb), desiredDb);

        var result = ChangeScriptDecorator.Decorate(diff, [script]);

        result.Diagnostics.ShouldHaveSingleItem().ShouldBe(DiffDiagnostics.DeadMigration(script));
    }

    [Fact]
    public void AttachedScript_ReportsNoDiagnostics()
    {
        var script = Change(ChangeTrigger.AddColumn, "email");
        var currentDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [Users(Id)] }] };
        var desiredDb = new Database { Schemas = [new Schema { Name = new SqlIdentifier("app"), Tables = [Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text })] }] };
        var diff = _sut.Compare(AlignedDatabase.Unaligned(currentDb), desiredDb);

        var result = ChangeScriptDecorator.Decorate(diff, [script]);

        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void AlterColumnType_DoesNotAttachToANullabilityOnlyChange()
    {
        // The column changes, but not its type, so a type-change script has nothing to prepare.
        var script = Change(ChangeTrigger.AlterColumnType, "email");
        var diff = Diff(
            Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text, IsNullable = true }),
            Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text, IsNullable = false }),
            script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void Attachment_MatchesSchemaTableAndMemberCaseInsensitively()
    {
        var script = new ChangeScript(new SqlIdentifier("backfill"), new SqlText("UPDATE 1"), new SqlIdentifier("APP"),
            ChangeTrigger.AddColumn, new SqlIdentifier("Users"), new SqlIdentifier("EMAIL"));
        var diff = Diff(Users(Id), Users(Id, new Column { Name = new SqlIdentifier("email"), Type = SqlType.Text }), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(script);
    }
}
