using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Extensions;

namespace NSchema.Diff.Domain.Models.Extensions;

/// <summary>
/// Describes a change to a database extension.
/// </summary>
/// <param name="Name">The extension name.</param>
/// <param name="Kind">The change to the extension.</param>
/// <param name="Definition">The extension definition for an added extension; otherwise <see langword="null"/>.</param>
/// <param name="Version">The change to the extension's version, if any.</param>
/// <param name="Comment">The change to the extension's comment, if any.</param>
public sealed record ExtensionDiff(
    SqlIdentifier Name,
    ChangeKind Kind,
    Extension? Definition = null,
    ValueChange<string>? Version = null,
    ValueChange<string>? Comment = null
) : INamedObjectDiff;
