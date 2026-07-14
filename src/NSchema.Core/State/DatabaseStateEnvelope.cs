using NSchema.State.Domain.Models;
using NSchema.Project.Domain.Models;

namespace NSchema.State;

/// <summary>
/// The versioned wrapper persisted by the state store.
/// </summary>
/// <param name="Version">The version of the envelope format.</param>
/// <param name="Database">The captured database structure..</param>
internal sealed record DatabaseStateEnvelope(int Version, Database Database)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The recorded script executions.
    /// </summary>
    public IReadOnlyList<ScriptExecution> Scripts { get; init; } = [];
}
