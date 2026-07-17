using Microsoft.Extensions.Logging.Abstractions;
using NSchema.Diff.Model.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Constraints;
using NSchema.Model.Schemas;
using NSchema.Model.Scripts;
using NSchema.Model.Tables;
using NSchema.Project.Model.Directives;
using DatabaseComparer = NSchema.Diff.Model.Services.DatabaseComparer;

namespace NSchema.Tests.Diff;

/// <summary>
/// The change-event script attachment folded into the structural compare: a change script rides the diff node
/// it accompanies. Driven through the comparer, since attachment happens in the per-table pass.
/// </summary>
public class ChangeScriptAttachmentTests
{
    private readonly DatabaseComparer _sut = new(NullLogger<DatabaseComparer>.Instance);

    /// <summary>Diffs the given current/desired <c>app.users</c> tables, steered by the given change scripts.</summary>
    private TableDiff Diff(Table current, Table desired, params ChangeScript[] scripts)
    {
        var directives = new ProjectDirectives(ChangeScripts: scripts);
        var currentDb = new Database([new Schema(new SqlIdentifier("app"), Tables: [current])]);
        var desiredDb = new Database([new Schema(new SqlIdentifier("app"), Tables: [desired])]);
        return _sut.Compare(currentDb, desiredDb, directives).Schemas.Single().Tables.Single();
    }

    private static Table Users(params object[] members)
    {
        var columns = members.OfType<Column>().ToList();
        var pks = members.OfType<PrimaryKey>().ToList();
        var fks = members.OfType<ForeignKey>().ToList();
        var uqs = members.OfType<UniqueConstraint>().ToList();
        var cks = members.OfType<CheckConstraint>().ToList();
        var exs = members.OfType<ExclusionConstraint>().ToList();
        return new Table(new SqlIdentifier("users"), Columns: columns, PrimaryKey: pks.FirstOrDefault(),
            ForeignKeys: fks, UniqueConstraints: uqs, CheckConstraints: cks, ExclusionConstraints: exs);
    }

    private static Column Id => new(new SqlIdentifier("id"), SqlType.Int);

    private static ChangeScript Change(ChangeTrigger trigger, string member, string? name = null) =>
        new(new SqlIdentifier(name ?? member), new SqlText($"UPDATE app.users -- {member}"), new SqlIdentifier("app"),
            trigger, new SqlIdentifier("users"), new SqlIdentifier(member));

    [Fact]
    public void AddColumn_AttachesToTheAddedColumn()
    {
        var script = Change(ChangeTrigger.AddColumn, "email");
        var diff = Diff(Users(Id), Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text)), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AlterColumnType_AttachesToTheRetypedColumn()
    {
        var script = Change(ChangeTrigger.AlterColumnType, "total");
        var diff = Diff(
            Users(Id, new Column(new SqlIdentifier("total"), SqlType.Text)),
            Users(Id, new Column(new SqlIdentifier("total"), SqlType.Int)),
            script);

        diff.Columns.Single(c => c.Name.Value == "total").MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedPrimaryKey()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_pk");
        var diff = Diff(
            Users(Id),
            Users(Id, new PrimaryKey(new SqlIdentifier("users_pk"), [new SqlIdentifier("id")])),
            script);

        diff.PrimaryKey.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedUniqueConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_email_uq");
        var email = new Column(new SqlIdentifier("email"), SqlType.Text);
        var diff = Diff(
            Users(Id, email),
            Users(Id, email, new UniqueConstraint(new SqlIdentifier("users_email_uq"), [new SqlIdentifier("email")])),
            script);

        diff.UniqueConstraints.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedForeignKey()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_org_fk");
        var orgId = new Column(new SqlIdentifier("org_id"), SqlType.Int);
        var diff = Diff(
            Users(Id, orgId),
            Users(Id, orgId, new ForeignKey(new SqlIdentifier("users_org_fk"), [new SqlIdentifier("org_id")], new SqlIdentifier("app"), new SqlIdentifier("orgs"), [new SqlIdentifier("id")])),
            script);

        diff.ForeignKeys.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedCheckConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "users_age_chk");
        var age = new Column(new SqlIdentifier("age"), SqlType.Int);
        var diff = Diff(
            Users(Id, age),
            Users(Id, age, new CheckConstraint(new SqlIdentifier("users_age_chk"), new SqlText("age >= 0"))),
            script);

        diff.Checks.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void AddConstraint_AttachesToTheAddedExclusionConstraint()
    {
        var script = Change(ChangeTrigger.AddConstraint, "no_overlap");
        var during = new Column(new SqlIdentifier("during"), SqlType.Text);
        var diff = Diff(
            Users(Id, during),
            Users(Id, during, new ExclusionConstraint(new SqlIdentifier("no_overlap"), [new ExclusionElement("&&", new SqlIdentifier("during"))], "gist")),
            script);

        diff.ExclusionConstraints.Single().MigrationScript.ShouldBe(script);
    }

    [Fact]
    public void WrongMemberName_DoesNotAttach()
    {
        var script = Change(ChangeTrigger.AddColumn, "phone");
        var diff = Diff(Users(Id), Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text)), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void WrongTrigger_DoesNotAttach()
    {
        // An AlterColumnType script targeting a column that is being added, not retyped.
        var script = Change(ChangeTrigger.AlterColumnType, "email");
        var diff = Diff(Users(Id), Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text)), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void AlterColumnType_DoesNotAttachToANullabilityOnlyChange()
    {
        // The column changes, but not its type, so a type-change script has nothing to prepare.
        var script = Change(ChangeTrigger.AlterColumnType, "email");
        var diff = Diff(
            Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text, IsNullable: true)),
            Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text, IsNullable: false)),
            script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBeNull();
    }

    [Fact]
    public void Attachment_MatchesSchemaTableAndMemberCaseInsensitively()
    {
        var script = new ChangeScript(new SqlIdentifier("backfill"), new SqlText("UPDATE 1"), new SqlIdentifier("APP"),
            ChangeTrigger.AddColumn, new SqlIdentifier("Users"), new SqlIdentifier("EMAIL"));
        var diff = Diff(Users(Id), Users(Id, new Column(new SqlIdentifier("email"), SqlType.Text)), script);

        diff.Columns.Single(c => c.Name.Value == "email").MigrationScript.ShouldBe(script);
    }
}
