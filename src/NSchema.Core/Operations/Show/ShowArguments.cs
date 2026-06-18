namespace NSchema.Operations.Show;

/// <summary>
/// Arguments for an <see cref="IShowOperation"/> run.
/// </summary>
public sealed record ShowArguments
{
    /// <summary>
    /// The schemas to scope the output to. When <see langword="null"/>, the whole recorded state is shown.
    /// Ignored when <see cref="PlanFile"/> is set (a saved plan is a fixed artifact).
    /// </summary>
    public string[]? Schemas { get; init; }

    /// <summary>
    /// A saved plan file to show instead of the recorded state. When set, the operation reads the file and
    /// reports its diff, plan, and SQL — without contacting the live database or the state store. When
    /// <see langword="null"/>, the recorded (offline) state is shown.
    /// </summary>
    public string? PlanFile { get; init; }
}
