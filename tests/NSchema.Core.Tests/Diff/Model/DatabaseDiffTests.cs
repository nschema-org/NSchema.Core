using NSchema.Project.Domain.Models;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.Diff.Model;

public sealed class DatabaseDiffTests
{
    private static readonly DatabaseDiff _diff = new DatabaseDiff([])
    {
        Scripts = [new Script(new SqlIdentifier("Backfill Emails"), "SELECT 1;", new DeploymentEvent(DeploymentPhase.Pre))],
    };

    [Fact]
    public void FindScript_ResolvesByName_CaseInsensitively()
        => _diff.FindScript(new SqlIdentifier("backfill emails")).ShouldBe(_diff.Scripts[0]);

    [Fact]
    public void FindScript_UnknownName_ReturnsNull()
        => _diff.FindScript(new SqlIdentifier("nope")).ShouldBeNull();
}
