using NSchema.Project.Domain.Models;
using NSchema.Current.Domain.Models;

namespace NSchema.Current.Storage;

/// <summary>
/// The versioned wrapper persisted by the state store.
/// </summary>
/// <param name="Version">The version of the envelope format.</param>
/// <param name="Schema">The captured database schema.</param>
internal sealed record SchemaStateEnvelope(int Version, DatabaseSchema Schema)
{
    /// <summary>
    /// The current envelope format version.
    /// </summary>
    public const int CurrentVersion = 1;

    /// <summary>
    /// The recorded script executions.
    /// </summary>
    public IReadOnlyList<ScriptExecution> ExecutedScripts { get; init; } = [];
}
