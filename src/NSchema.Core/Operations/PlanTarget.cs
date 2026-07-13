namespace NSchema.Operations;

/// <summary>
/// What a plan computes — the current schema a forward migration is diffed against, or a teardown.
/// </summary>
public enum PlanTarget
{
    /// <summary>
    /// A forward migration against the recorded state, falling back to the live database when nothing is recorded.
    /// Used to preview.
    /// </summary>
    Recorded,

    /// <summary>
    /// A forward migration against the live database (required). Used when the plan is about to be applied.
    /// </summary>
    Live,

    /// <summary>
    /// A teardown of the managed schema.
    /// </summary>
    /// <remarks>
    /// Unlike a forward migration it has no current-schema source choice: the managed schema is defined by the
    /// recorded state, never the live database, so a teardown is always computed against that offline snapshot,
    /// and <see cref="PlanArguments.Scope"/> does not apply.
    /// </remarks>
    Teardown,
}
