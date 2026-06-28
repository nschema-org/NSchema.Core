using NSchema.Diagnostics;

namespace NSchema.Operations.Validate;

/// <summary>
/// Loads the desired schema and validates it against the configured schema policies, without planning or applying.
/// </summary>
internal interface IValidateOperation
{
    /// <summary>
    /// Executes the validate operation, returning the aggregated validation diagnostics.
    /// </summary>
    /// <param name="arguments">The arguments controlling the validation.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    Task<Result> Execute(ValidateArguments arguments, CancellationToken cancellationToken = default);
}
