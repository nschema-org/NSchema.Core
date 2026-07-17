using NSchema.Model;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// A partial-schema directive: the schema's declaration is partial, so absence does not mean removal.
/// </summary>
/// <param name="Schema">The declared schema the directive marks.</param>
public sealed record SchemaPartialDirective(SqlIdentifier Schema);
