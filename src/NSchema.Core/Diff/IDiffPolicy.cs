using NSchema.Diagnostics;
using NSchema.Diff.Model;

namespace NSchema.Diff;

/// <summary>
/// Defines an interface for validating the structured <see cref="DatabaseDiff"/> against a set of rules
/// or policies. Diff policies reason about the change at the schema/table/column level (for example,
/// flagging destructive removals) before the diff is linearized into executable actions.
/// </summary>
public interface IDiffPolicy
{
    /// <summary>
    /// Validates the given diff against the rules defined by this policy and returns a collection of any
    /// errors found during validation.
    /// </summary>
    /// <param name="diff">The structured migration diff to validate against this policy.</param>
    /// <returns>The collection of errors found during validation. If the diff is valid according to this policy, the collection will be empty.</returns>
    IEnumerable<Diagnostic> Validate(DatabaseDiff diff);
}
