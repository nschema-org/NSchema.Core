using NSchema.Schema.Model;

namespace NSchema.Schema.Templates;

/// <summary>
/// The outcome of expanding template applications.
/// </summary>
/// <param name="Schema">The schema with every template application merged in.</param>
/// <param name="Scripts">The instantiated scripts, each paired with the schema it was instantiated for.</param>
internal sealed record TemplateExpansion(
    DatabaseSchema Schema,
    IReadOnlyList<TemplateScriptInstance> Scripts
);
