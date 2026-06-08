namespace NSchema.Operations.Services;

/// <summary>
/// Controls how <see cref="IMigrationWorkflow.Refresh"/> behaves when no state store is configured.
/// </summary>
internal enum RefreshMode
{
    /// <summary>A state store must be configured; refresh throws if it isn't.</summary>
    Required,

    /// <summary>Refresh is skipped silently when no state store is configured.</summary>
    Optional,
}
