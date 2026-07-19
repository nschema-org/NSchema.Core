using NSchema.Project.Nsql;

namespace NSchema.Config;

/// <summary>
/// The diagnostics minted when assembling a configuration.
/// </summary>
internal static class ConfigDiagnostics
{
    private const string Source = "config";

    /// <summary>
    /// A statement missing an attribute its grammar requires.
    /// </summary>
    public static NsqlDiagnostic RequiredAttribute(string statement, string attribute, SourcePosition position) =>
        new(Source, $"A {statement:text} statement requires a '{attribute}' attribute.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A grammar-owned attribute whose value is not a string.
    /// </summary>
    public static NsqlDiagnostic AttributeMustBeString(string statement, string attribute, SourcePosition position) =>
        new(Source, $"The {statement:text} '{attribute}' attribute must be a string.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// An attribute a closed-attribute statement does not define.
    /// </summary>
    public static NsqlDiagnostic UnknownAttribute(string statement, string attribute, string known, SourcePosition position) =>
        new(Source, $"A {statement:text} statement has no '{attribute}' attribute; it takes {known:text}.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A plugin source that is not a valid package id.
    /// </summary>
    public static NsqlDiagnostic InvalidPackageId(string source, SourcePosition position) =>
        new(Source, $"'{source}' is not a valid package id.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A version attribute that parses as neither a version nor a version range.
    /// </summary>
    public static NsqlDiagnostic InvalidVersionRange(string version, SourcePosition position) =>
        new(Source, $"'{version}' is not a valid version or version range.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A plugin label declared by more than one <c>PLUGIN</c> statement.
    /// </summary>
    public static NsqlDiagnostic DuplicatePluginLabel(string label, SourcePosition position) =>
        new(Source, $"Plugin '{label}' is declared more than once.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A package declared by more than one <c>PLUGIN</c> statement.
    /// </summary>
    public static NsqlDiagnostic DuplicatePluginSource(string source, SourcePosition position) =>
        new(Source, $"Package '{source}' is declared by more than one PLUGIN statement; a package is declared once and referenced by its label.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A second statement of a kind the configuration holds at most one of.
    /// </summary>
    public static NsqlDiagnostic DuplicateStatement(string statement, SourcePosition position) =>
        new(Source, $"A configuration holds at most one {statement:text} statement.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A <c>DATABASE</c>/<c>STATE</c> statement with no label, so it names no plugin.
    /// </summary>
    public static NsqlDiagnostic UnlabelledReference(string statement, SourcePosition position) =>
        new(Source, $"A {statement:text} statement needs a label naming the plugin that serves it.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A <c>DATABASE</c>/<c>STATE</c> label that no <c>PLUGIN</c> statement (or host built-in) declares.
    /// </summary>
    public static NsqlDiagnostic UnknownPluginLabel(string statement, string label, SourcePosition position) =>
        new(Source, $"{statement:text} references plugin '{label}', but no PLUGIN statement declares it.", DiagnosticSeverity.Error, position);

    /// <summary>
    /// A host version outside the range the project's <c>ENGINE</c> statement requires.
    /// </summary>
    public static Diagnostic EngineRequirementUnsatisfied(VersionRange range, SemanticVersion version) => Diagnostic.Error(Source,
        $"This project requires an engine version matching '{range}', but this engine is {version}. Update the engine, or relax the ENGINE assertion.");
}
