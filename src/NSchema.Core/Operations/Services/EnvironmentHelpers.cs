namespace NSchema.Operations.Services;

/// <summary>
/// Helper methods for determining environment capabilities.
/// </summary>
internal static class EnvironmentHelpers
{
    /// <summary>
    /// Determines whether the environment supports color.
    /// </summary>
    public static bool SupportsColor => string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR")) && !Console.IsOutputRedirected;
}
