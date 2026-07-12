using NSchema.Diff.Model;
using NSchema.Schema.Model.Scripts;

namespace NSchema.Tests.Diff.Model;

public sealed class DatabaseDiffTests
{
    private static readonly DatabaseDiff _diff = new DatabaseDiff([])
    {
        Scripts = [new Script("Backfill Emails", "SELECT 1;", new DeploymentEvent(DeploymentPhase.Pre))],
    };

    [Fact]
    public void FindScript_ResolvesByName_CaseInsensitively()
        => _diff.FindScript("backfill emails").ShouldBe(_diff.Scripts[0]);

    [Fact]
    public void FindScript_UnknownName_ReturnsNull()
        => _diff.FindScript("nope").ShouldBeNull();
}
