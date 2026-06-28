namespace NSchema.Operations.Apply;

/// <summary>
/// The result of applying a plan. Carries no data today; it exists so the operation can grow a payload without a
/// breaking signature change.
/// </summary>
public sealed record ApplyResult;
