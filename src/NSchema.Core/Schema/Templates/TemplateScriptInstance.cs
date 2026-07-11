using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Templates;

/// <summary>
/// A script instantiated by a template application, paired with the schema it was instantiated for.
/// </summary>
/// <param name="SchemaName">The schema the template application instantiated the script for.</param>
/// <param name="Script">The instantiated script, with the <c>{schema}</c> token already substituted.</param>
internal sealed record TemplateScriptInstance(string SchemaName, Script Script);
