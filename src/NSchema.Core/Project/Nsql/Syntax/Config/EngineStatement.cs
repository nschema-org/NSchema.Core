namespace NSchema.Project.Nsql.Syntax.Config;

/// <summary>
/// <c>ENGINE ( version = '…' );</c> — asserts the engine version the project requires. The statement carries
/// no label: there is only one engine, and it is the host's, so the assertion is evaluated by the host
/// against its own version — never implicitly by the engine itself.
/// </summary>
/// <param name="Attributes">The attribute list.</param>
public sealed record EngineStatement(IReadOnlyList<ConfigAttribute> Attributes) : ConfigStatement(null, Attributes);
