using NSchema.Model;

namespace NSchema.Project.Model.Services;

/// <summary>
/// An <c>INCLUDE name</c> member inside a table body, targeting the table it was written in by name.
/// </summary>
/// <param name="SchemaName">The schema of the including table (the placeholder for a table declared inside a schema template).</param>
/// <param name="TableName">The name of the including table.</param>
/// <param name="TemplateName">The name of the table template to include.</param>
/// <param name="ColumnPosition">The index among the table's declared columns where the include appeared.</param>
internal sealed record TemplateInclude(SqlIdentifier SchemaName, SqlIdentifier TableName, SqlIdentifier TemplateName, int ColumnPosition);
