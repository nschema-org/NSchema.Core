using Microsoft.Extensions.Options;
using NSchema.Diff;
using NSchema.Diff.Model;

namespace NSchema.Tests.Migration;

public class TerraformDiffRendererTests
{
    private static MigrationDiff Diff() =>
        new([new SchemaDiff("app", ChangeKind.Add, null, null, [], [])], [], []);

    private static TerraformDiffRenderer Renderer(bool includeColour) =>
        new(Options.Create(new TerraformDiffRendererOptions { IncludeColour = includeColour }));

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
