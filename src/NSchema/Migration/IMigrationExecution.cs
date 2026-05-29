namespace NSchema.Migration;

/// <summary>
/// A migration plan compiled into a concrete, inspectable unit of work by an <see cref="IMigrationExecutor"/>.
/// </summary>
/// <remarks>
/// The same compiled instance is both previewed and executed, so what a caller inspects via
/// <see cref="Preview"/> is exactly what <see cref="Execute"/> performs. What "execution" means is
/// executor-specific: running SQL against a database, writing a schema file, uploading scripts, and so on.
/// </remarks>
public interface IMigrationExecution
{
    /// <summary>
    /// A human-readable representation of what executing this unit would do — for example the SQL
    /// statements that would run, or the contents of a file that would be written.
    /// </summary>
    IReadOnlyList<string> Preview { get; }

    /// <summary>
    /// Performs the compiled unit of work against the target.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task Execute(CancellationToken cancellationToken = default);
}
