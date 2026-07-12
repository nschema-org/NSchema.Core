namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// An <c>INCLUDE name</c> member inside a table body, targeting the table it was written in by name.
/// </summary>
/// <param name="SchemaName">The schema of the including table (the placeholder for a table declared inside a schema template).</param>
/// <param name="TableName">The name of the including table.</param>
/// <param name="TemplateName">The name of the table template to include.</param>
/// <param name="ColumnPosition">The index among the table's declared columns where the include appeared.</param>
public sealed record TemplateInclude(string SchemaName, string TableName, string TemplateName, int ColumnPosition);
