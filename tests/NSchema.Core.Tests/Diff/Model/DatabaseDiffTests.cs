using NSchema.Diff.Domain.Models;
using NSchema.Diff.Domain.Models.Columns;
using NSchema.Diff.Domain.Models.Schemas;
using NSchema.Diff.Domain.Models.Tables;
using NSchema.Model;
using NSchema.Model.Columns;
using NSchema.Model.Scripts;

namespace NSchema.Tests.Diff.Model;

public sealed class DatabaseDiffTests
{
    private static DeploymentScript Deployment(string name) =>
        new(new SqlIdentifier(name), new SqlText("SELECT 1;"), null, DeploymentPhase.Pre);

    private static ChangeScript Change(string name) =>
        new(new SqlIdentifier(name), new SqlText("UPDATE 1;"), new SqlIdentifier("app"),
            ChangeTrigger.AddColumn, new SqlIdentifier("users"), new SqlIdentifier("email"));

    private static DatabaseDiff WithChangeScript(ChangeScript change)
    {
        var column = new ColumnDiff(new SqlIdentifier("email"), ChangeKind.Add, new Column(new SqlIdentifier("email"), SqlType.Text)) { MigrationScript = change };
        var table = new TableDiff(new SqlIdentifier("app"), new SqlIdentifier("users"), ChangeKind.Modify, Columns: [column]);
        return new DatabaseDiff([new SchemaDiff(new SqlIdentifier("app"), Tables: [table])]);
    }

    [Fact]
    public void ChangeScripts_WalksTheNodesTheyRideOn()
    {
        var change = Change("backfill");

        WithChangeScript(change).ChangeScripts().ShouldBe(new[] { change });
    }

    [Fact]
    public void AllScripts_IsChangeScriptsThenDeploymentScripts()
    {
        var change = Change("backfill");
        var deploy = Deployment("seed");
        var diff = WithChangeScript(change) with { DeploymentScripts = [deploy] };

        diff.AllScripts().ShouldBe(new Script[] { change, deploy });
    }

    [Fact]
    public void IsEmpty_TrueForNoChangesAndNoDeploymentScripts()
        => new DatabaseDiff([]).IsEmpty.ShouldBeTrue();

    [Fact]
    public void IsEmpty_FalseWhenADeploymentScriptRuns()
    {
        var diff = new DatabaseDiff([]) with { DeploymentScripts = [Deployment("seed")] };

        diff.IsEmpty.ShouldBeFalse();
    }
}
