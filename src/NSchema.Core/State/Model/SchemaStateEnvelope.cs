using NSchema.Schema.Model;

namespace NSchema.State.Model;

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
    public IReadOnlyList<ScriptExecutionRecord> ExecutedScripts { get; init; } = [];
}
