namespace NSchema.Project.Domain;

/// <summary>
/// The diagnostics minted while applying templates and resolving includes.
/// </summary>
internal static class TemplateDiagnostics
{
    private const string Source = "templates";

    /// <summary>
    /// The same template name declared more than once.
    /// </summary>
    public static Diagnostic DuplicateTemplate(string name) => Diagnostic.Error(Source,
        $"Duplicate template '{name}' declared.");

    /// <summary>
    /// An APPLY TEMPLATE referencing a template that does not exist.
    /// </summary>
    public static Diagnostic UnknownTemplate(string name) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE references unknown template '{name}'.");

    /// <summary>
    /// An APPLY TEMPLATE naming a table template (those are consumed via INCLUDE).
    /// </summary>
    public static Diagnostic AppliedTableTemplate(string name) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE targets schemas, but '{name}' is a table template; include it from a table body with INCLUDE.");

    /// <summary>
    /// An APPLY TEMPLATE naming a schema the project does not declare.
    /// </summary>
    public static Diagnostic UnknownTargetSchema(string templateName, string schemaName) => Diagnostic.Error(Source,
        $"APPLY TEMPLATE '{templateName}' targets unknown schema '{schemaName}'; declare it with CREATE SCHEMA.");

    /// <summary>
    /// An INCLUDE inside a template body targeting a table that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTable(string templateName, string schemaName, string tableName) => Diagnostic.Error(Source,
        $"INCLUDE '{templateName}' targets unknown table '{schemaName}.{tableName}'.");

    /// <summary>
    /// A table INCLUDE naming a template that does not exist.
    /// </summary>
    public static Diagnostic IncludeUnknownTemplate(string qualifiedTable, string templateName) => Diagnostic.Error(Source,
        $"Table '{qualifiedTable}' includes unknown template '{templateName}'.");

    /// <summary>
    /// A table INCLUDE naming a schema template (only FOR TABLE templates can be included).
    /// </summary>
    public static Diagnostic IncludedSchemaTemplate(string qualifiedTable, string templateName) => Diagnostic.Error(Source,
        $"Table '{qualifiedTable}' includes '{templateName}', which is a schema template; only a FOR TABLE template can be included.");

    /// <summary>
    /// An included template adding a column the table already declares.
    /// </summary>
    public static Diagnostic IncludeColumnConflict(string templateName, string columnName, string qualifiedTable) => Diagnostic.Error(Source,
        $"Template '{templateName}' adds column '{columnName}' to '{qualifiedTable}', which already declares it.");

    /// <summary>
    /// An included template adding a primary key to a table that already declares one.
    /// </summary>
    public static Diagnostic IncludePrimaryKeyConflict(string templateName, string qualifiedTable) => Diagnostic.Error(Source,
        $"Template '{templateName}' adds a primary key to '{qualifiedTable}', which already declares one.");
}
