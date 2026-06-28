using NSchema.Diagnostics;

namespace NSchema.Operations.Import;

/// <summary>
/// Reads the live database schema and writes it to the configured import target.
/// </summary>
internal interface IImportOperation
{
    /// <summary>
    /// Executes the import operation.
    /// </summary>
    /// <param name="arguments">The arguments controlling which schema to import.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Execute(ImportArguments arguments, CancellationToken cancellationToken = default);
}
