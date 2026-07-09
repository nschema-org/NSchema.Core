using NSchema.Schema.Model;
using NSchema.Schema.Model.Migrations;

namespace NSchema.Schema.Templates;

/// <summary>
/// The outcome of expanding template applications.
/// </summary>
/// <param name="Schema">The schema with every template application merged in.</param>
/// <param name="Migrations">The instantiated data migrations, re-homed to their applied schemas.</param>
/// <param name="Scripts">The instantiated deployment scripts, paired with their origin schemas.</param>
internal sealed record TemplateExpansion(
    DatabaseSchema Schema,
    IReadOnlyList<DataMigration> Migrations,
    IReadOnlyList<TemplateScriptInstance> Scripts
);
