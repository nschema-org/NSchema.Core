namespace NSchema.Current;

/// <summary>
/// The diagnostics minted by the current-schema provider.
/// </summary>
internal static class CurrentDiagnostics
{
    private const string Source = "current";

    /// <summary>
    /// An online read without a registered live database provider.
    /// </summary>
    public static Diagnostic NoOnlineSource =>
        Diagnostic.Error(Source, "No live database provider is registered to read the online schema.");

    /// <summary>
    /// An offline read without a registered state store.
    /// </summary>
    public static Diagnostic NoOfflineSource =>
        Diagnostic.Error(Source, "No state store is registered to read the recorded schema.");
}
