namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// The granularity a template targets.
/// </summary>
public enum TemplateKind
{
    /// <summary>
    /// The template declares whole objects.
    /// </summary>
    Schema,

    /// <summary>
    /// The template declares table members.
    /// </summary>
    Table,
}
