using NSchema.Project.Domain.Models;
namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// A template application statement.
/// </summary>
/// <param name="TemplateName">The name of the template to instantiate.</param>
/// <param name="SchemaNames">The schemas to instantiate the template into.</param>
public sealed record TemplateApplication(SqlIdentifier TemplateName, IReadOnlyList<SqlIdentifier> SchemaNames);
