using System.Diagnostics;

namespace NSchema.Project.Domain.Models.Extensions;

/// <summary>
/// Represents a database extension.
/// </summary>
/// <param name="Name">The extension name.</param>
/// <param name="Version">The requested version, or <see langword="null"/> to accept whatever the provider installs.</param>
/// <param name="Comment">An optional comment or description for the extension.</param>
[DebuggerDisplay("{Name,nq} (extension)")]
public sealed record Extension(
    string Name,
    string? Version = null,
    string? Comment = null
) : INamedObject;
