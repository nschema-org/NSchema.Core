namespace NSchema.Operations.Validate;

/// <summary>
/// Arguments for an <see cref="IValidateOperation"/> run.
/// </summary>
public sealed record ValidateArguments
{
    /// <summary>
    /// The schemas to scope validation to. When <see langword="null"/>, scope is derived from the desired schema.
    /// </summary>
    public string[]? Schemas { get; init; }
}
