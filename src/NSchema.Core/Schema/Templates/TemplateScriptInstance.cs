using NSchema.Schema.Model.Scripts;

namespace NSchema.Schema.Templates;

/// <summary>
/// A deployment script instantiated by a template application, paired with the schema it was instantiated for
/// so a scoped read can drop out-of-scope instances. The schema never rides on the script itself — deployment
/// scripts are schema-less by model; the pairing exists only between expansion and aggregation.
/// </summary>
/// <param name="SchemaName">The schema the template application instantiated the script for.</param>
/// <param name="Script">The instantiated script, with the <c>{schema}</c> token already substituted.</param>
internal sealed record TemplateScriptInstance(string SchemaName, Script Script);
