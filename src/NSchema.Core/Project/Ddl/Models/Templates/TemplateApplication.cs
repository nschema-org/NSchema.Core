namespace NSchema.Project.Ddl.Models.Templates;

/// <summary>
/// A template application statement.
/// </summary>
/// <param name="TemplateName">The name of the template to instantiate.</param>
/// <param name="SchemaNames">The schemas to instantiate the template into.</param>
public sealed record TemplateApplication(string TemplateName, IReadOnlyList<string> SchemaNames);
