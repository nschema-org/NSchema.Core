namespace NSchema.Config;

/// <summary>
/// The engine-level configuration an <c>ENGINE</c> statement declares.
/// </summary>
/// <param name="Requirement">The engine version assertion.</param>
public sealed record EngineConfig(EngineRequirement Requirement);
