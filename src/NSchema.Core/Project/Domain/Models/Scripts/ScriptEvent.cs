namespace NSchema.Project.Domain.Models.Scripts;

/// <summary>
/// The event a script runs on.
/// </summary>
public abstract record ScriptEvent
{
    /// <summary>
    /// The schema the script's run is scoped to, or <see langword="null"/> when the script is global.
    /// </summary>
    public string? ScopeSchema { get; init; }

    /// <summary>
    /// The event as written in DDL source.
    /// </summary>
    public abstract string Description { get; }
}
