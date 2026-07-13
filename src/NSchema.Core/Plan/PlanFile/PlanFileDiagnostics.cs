namespace NSchema.Plan.PlanFile;

/// <summary>
/// The diagnostics minted when reading saved plan files.
/// </summary>
internal static class PlanFileDiagnostics
{
    private const string Source = "plan-file";

    /// <summary>A plan file that could not be read from disk.</summary>
    public static Diagnostic UnreadableFile(string path, Exception exception) => Diagnostic.Error(Source,
        $"Could not read '{path}': {exception.Message}");

    /// <summary>A plan file whose payload could not be deserialized.</summary>
    public static Diagnostic InvalidPayload(string path, Exception exception) => Diagnostic.Error(Source,
        $"{path}: {exception.Message}");
}
