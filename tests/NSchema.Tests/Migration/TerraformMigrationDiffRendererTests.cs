using Microsoft.Extensions.Options;
using NSchema.Migration;
using NSchema.Migration.Diff;
using NSchema.Migration.Plan;
using NSchema.Schema;

namespace NSchema.Tests.Migration;

public class TerraformMigrationDiffRendererTests
{
    private static MigrationDiff Diff() =>
        new DefaultMigrationDiffBuilder().Build(new MigrationPlan([new CreateSchema("app")], DatabaseSchema.Create([])));

    private static TerraformMigrationDiffRenderer Renderer(bool includeColour) =>
        new(Options.Create(new TerraformRendererOptions { IncludeColour = includeColour }));

    [Fact]
    public void Render_WithColour_EmitsAnsiEscapeCodes()
    {
        var output = Renderer(includeColour: true).Render(Diff());

        output.ShouldContain("\x1b[32m"); // green marker for an addition
        output.ShouldContain("schema app");
    }

    [Fact]
    public void Render_WithoutColour_EmitsPlainMarkers()
    {
        var output = Renderer(includeColour: false).Render(Diff());

        output.ShouldNotContain("\x1b["); // no ANSI escape sequences at all
        output.ShouldContain("+ schema app");
    }
}
