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
    public static Diagnostic DuplicateChangeTarget(ChangeScript change) => Diagnostic.Error(Source,
        $"Duplicate migration for {ChangeScript.TriggerText(change.Trigger)} '{change.Path}' declared.");

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
    /// The same named object declared more than once within a schema.
    /// </summary>
    public static Diagnostic DuplicateObject(string kind, SqlIdentifier name, SqlIdentifier schemaName, string suffix) => Diagnostic.Error(Source,
        $"Duplicate {kind} '{name}' found in schema '{schemaName}'{suffix}.");

    // ── Directive rules — addresses arrive rendered because their shapes differ per kind. ──

    /// <summary>
    /// A rename whose target the project does not declare.
    /// </summary>
    public static Diagnostic RenameTargetNotDeclared(string kind, string address, SqlIdentifier to) => Diagnostic.Error(Source,
        $"RENAME {kind} '{address}' TO {to}: the project does not declare '{to}'. A rename pairs the current object with its declaration under the new name.");

    /// <summary>
    /// A rename whose previous name the project still declares.
    /// </summary>
    public static Diagnostic RenameSourceStillDeclared(string kind, string address, SqlIdentifier to) => Diagnostic.Error(Source,
        $"RENAME {kind} '{address}' TO {to}: the previous name is still declared, so the rename cannot be told apart from a retain-plus-create.");

    /// <summary>
    /// A rename of an object the project also drops.
    /// </summary>
    public static Diagnostic RenameOfDropped(string kind, string address) => Diagnostic.Error(Source,
        $"RENAME {kind} '{address}': the object is also dropped; a dropped object cannot be renamed.");

    /// <summary>
    /// A drop of an object the project also declares.
    /// </summary>
    public static Diagnostic DropOfDeclared(string kind, string address) => Diagnostic.Error(Source,
        $"DROP {kind} '{address}': the project also declares it. To recreate an object, drop it and redeclare it in separate migrations.");

    /// <summary>
    /// A directive addressing a schema the project does not declare.
    /// </summary>
    public static Diagnostic DirectiveSchemaNotDeclared(string directive, SqlIdentifier schema) => Diagnostic.Error(Source,
        $"{directive} addresses schema '{schema}', which the project does not declare.");

    /// <summary>
    /// A column rename addressing a table the project does not declare.
    /// </summary>
    public static Diagnostic DirectiveTableNotDeclared(MemberReference reference) => Diagnostic.Error(Source,
        $"RENAME COLUMN '{reference}' addresses a table the project does not declare.");

    /// <summary>
    /// A rename whose target is its own source.
    /// </summary>
    public static Diagnostic SelfRename(string kind, string address) => Diagnostic.Error(Source,
        $"RENAME {kind} '{address}': the target is the same name.");

    /// <summary>
    /// Two renames sharing a source.
    /// </summary>
    public static Diagnostic DuplicateRenameSource(string kind, string address) => Diagnostic.Error(Source,
        $"Multiple renames of {kind} '{address}' declared.");

    /// <summary>
    /// Two renames sharing a target.
    /// </summary>
    public static Diagnostic DuplicateRenameTarget(string kind, string address) => Diagnostic.Error(Source,
        $"Multiple renames of {kind} to '{address}' declared.");

    /// <summary>
    /// One rename's target being another's source — unordered, therefore ambiguous.
    /// </summary>
    public static Diagnostic RenameChain(string kind, string address) => Diagnostic.Error(Source,
        $"Renames of {kind} chain through '{address}'; renames are unordered, so a chain is ambiguous. Collapse it into a single rename.");
}
