using System.ComponentModel;
using Hamelin;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSchema.Build.Models;
using NSchema.Build.Services;

namespace NSchema.Build.Steps;

[DisplayName("Create GitHub Release")]
public class CreateRelease(
    ILogger<CreateRelease> logger,
    IOptions<BuildOptions> options,
    IPipelineContext context,
    ICommandRunner commands
) : IPipelineStep
{
    public async Task Run(CancellationToken cancellationToken = default)
    {
        var projectInfo = context.State.Get<ProjectInfo>() ?? throw new Exception("Project info not found in state.");
        var changelog = context.State.Get<ChangelogEntry>() ?? throw new Exception("Changelog entry not found in state.");

        var tag = $"v{projectInfo.Version}";
        var notesFile = context.FileSystem.CurrentDirectory
            .GetDirectory(options.Value.TempDirectory)
            .GetFile($"release-notes-{projectInfo.Version}.md");
        Directory.CreateDirectory(Path.GetDirectoryName(notesFile.AbsolutePath)!);
        await File.WriteAllTextAsync(notesFile.AbsolutePath, changelog.Body, cancellationToken);

        List<string> arguments = [
            "release", "create", tag,
            "--title", tag,
            "--notes-file", notesFile.AbsolutePath
        ];

        if (!string.IsNullOrEmpty(options.Value.CommitSha))
        {
            arguments.AddRange(["--target", options.Value.CommitSha]);
        }

        if (projectInfo.Version.IsPrerelease)
        {
            arguments.Add("--prerelease");
        }

        logger.LogInformation("Creating GitHub Release {Tag} (prerelease={IsPrerelease}).", tag, projectInfo.Version.IsPrerelease);
        await commands.Run("gh", arguments.ToArray(), cancellationToken);
    }
}
