using NSchema.Schema.Model.Functions;

namespace NSchema.Plan.Model.Functions;

/// <summary>
/// Represents the creation (or in-place body replacement) of a function. A provider emits
/// <c>CREATE OR REPLACE</c> so the same action serves an add and a definition-only modification.
/// </summary>
/// <param name="SchemaName">The name of the schema the function belongs to.</param>
/// <param name="Function">The definition of the function to create or replace.</param>
public sealed record CreateFunction(string SchemaName, Function Function) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
