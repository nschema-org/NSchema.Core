using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Project.Domain;

/// <summary>
/// The diagnostics minted while reading and aggregating the project.
/// </summary>
internal static class ProjectDiagnostics
{
    private const string Source = "project";

    /// <summary>
    /// No DDL file matched any registered project source.
    /// </summary>
    public static Diagnostic NoFilesMatched() => Diagnostic.Error(Source,
        "No SQL DDL files matched the registered schema sources.");

    /// <summary>
    /// Prefixes a read diagnostic with the file it came from — the reader knows the position, the caller knows the file.
    /// </summary>
    public static Diagnostic InFile(string path, Diagnostic diagnostic) =>
        diagnostic with { Message = $"{path}: {diagnostic.Message}" };

    /// <summary>
    /// A matched DDL file could not be read from disk.
    /// </summary>
    public static Diagnostic UnreadableFile(string path, Exception exception) => Diagnostic.Error(Source,
        $"Could not read '{path}': {exception.Message}");

    /// <summary>
    /// A script name declared more than once (names are the run-once and diagnostic identity).
    /// </summary>
    public static Diagnostic DuplicateScriptName(SqlIdentifier name) => Diagnostic.Error(Source,
        $"Duplicate script name '{name}' declared. Script names must be unique across the project; " +
        $"a script declared in a template applied to multiple schemas can include the {SchemaToken.Token} token in its name.");

    /// <summary>
    /// Two change-event scripts declared for the same trigger and path.
    /// </summary>
    public static Diagnostic DuplicateChangeTarget(ChangeEvent change) => Diagnostic.Error(Source,
        $"Duplicate migration for {ChangeEvent.TriggerText(change.Trigger)} '{change.Path}' declared.");

    /// <summary>
    /// A database-global extension declared more than once.
    /// </summary>
    public static Diagnostic DuplicateExtension(SqlIdentifier name) => Diagnostic.Error(Source,
        $"Duplicate extension '{name}' declared.");

    /// <summary>
    /// Two declarations of the same schema carry different comments.
    /// </summary>
    public static Diagnostic ConflictingComments(SqlIdentifier schemaName) => Diagnostic.Error(Source,
        $"Conflicting comments specified for schema '{schemaName}'.");

    /// <summary>
    /// Two declarations of the same schema carry different rename hints.
    /// </summary>
    public static Diagnostic ConflictingOldNames(SqlIdentifier schemaName) => Diagnostic.Error(Source,
        $"Conflicting old names specified for schema '{schemaName}'.");

    /// <summary>
    /// The same named object declared more than once within a schema.
    /// </summary>
    public static Diagnostic DuplicateObject(string kind, SqlIdentifier name, SqlIdentifier schemaName, string suffix) => Diagnostic.Error(Source,
        $"Duplicate {kind} '{name}' found in schema '{schemaName}'{suffix}.");
}
