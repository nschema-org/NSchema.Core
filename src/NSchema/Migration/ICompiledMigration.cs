namespace NSchema.Migration;

/// <summary>
/// A migration plan compiled into a concrete, inspectable unit of work by an <see cref="IMigrationCompiler"/>.
/// </summary>
public interface ICompiledMigration
{
    /// <summary>
    /// A human-readable representation of what executing this unit would do.
    /// </summary>
    IReadOnlyList<string> Preview { get; }

    /// <summary>
    /// Runs the compiled migration against the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(CancellationToken cancellationToken = default);
}
