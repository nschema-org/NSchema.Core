using NSchema.Model;

namespace NSchema.Project.Model.Directives;

/// <summary>
/// An extension drop directive: the extension is explicitly declared dropped.
/// </summary>
/// <param name="Name">The dropped extension's name.</param>
public sealed record ExtensionDropDirective(SqlIdentifier Name);
