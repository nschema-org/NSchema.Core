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
    /// A script declared more than once in the same scope (the address is the run-once and diagnostic identity).
    /// </summary>
    public static Diagnostic DuplicateScriptName(ScriptReference script) => Diagnostic.Error(Source,
        $"Duplicate script '{script}' declared. A script's name must be unique within its scope.");

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
