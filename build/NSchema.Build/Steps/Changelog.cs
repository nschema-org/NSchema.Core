using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;

namespace NSchema.Build.Steps;

[DisplayName("Validate Changelog Entry")]
public class Changelog(
    ILogger<Changelog> logger,
    IOptions<BuildOptions> options,
    IPipelineContext context
) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var projectInfo = context.State.Get<ProjectInfo>();
        if (projectInfo == null)
        {
            throw new Exception("Project info not found in state.");
        }

        var changelog = context.FileSystem.CurrentDirectory.GetFile(options.Value.ChangelogFile);
        if (!changelog.Exists)
        {
            throw new FileNotFoundException("Could not find changelog file", changelog.AbsolutePath);
        }

        var heading = $"## [{projectInfo.Version}]";
        var lines = await File.ReadAllLinesAsync(changelog.AbsolutePath, cancellationToken);

        var headingIndex = Array.FindIndex(lines, l => l.StartsWith(heading, StringComparison.Ordinal));
        if (headingIndex < 0)
        {
            throw new Exception(
                $"No changelog entry found for version {projectInfo.Version} in {options.Value.ChangelogFile}. Expected a heading starting with '{heading}'.");
        }

        var nextHeadingIndex = Array.FindIndex(lines, headingIndex + 1, l => l.StartsWith("## [", StringComparison.Ordinal));
        var endIndex = nextHeadingIndex < 0 ? lines.Length : nextHeadingIndex;
        var body = string.Join('\n', lines[(headingIndex + 1)..endIndex]).Trim();

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new Exception($"Changelog entry for version {projectInfo.Version} is empty.");
        }

        context.State.Set(new ChangelogEntry { Version = projectInfo.Version, Body = body });
        logger.LogInformation("Found changelog entry for {Version} ({Length} chars).", projectInfo.Version, body.Length);
    }
}
