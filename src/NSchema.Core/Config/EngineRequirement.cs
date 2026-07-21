using NSchema.Plugins.Model;

namespace NSchema.Config;

/// <summary>
/// The engine version a project asserts via its <c>ENGINE</c> statement.
/// </summary>
/// <param name="Version">The version range the host's version must fall within.</param>
public sealed record EngineRequirement(VersionRange Version)
{
    /// <summary>
    /// Validates the host's <paramref name="engineVersion"/> against the requirement: a failure when the
    /// version falls outside the required range.
    /// </summary>
    /// <param name="engineVersion">The host's own version.</param>
    public Result Validate(SemanticVersion engineVersion) => Version.Satisfies(engineVersion)
        ? Result.Success()
        : Result.From(ConfigDiagnostics.EngineRequirementUnsatisfied(Version, engineVersion));
}
