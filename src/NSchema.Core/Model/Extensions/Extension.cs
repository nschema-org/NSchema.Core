using System.Diagnostics;

namespace NSchema.Model.Extensions;

/// <summary>
/// Represents a database extension.
/// </summary>
/// <param name="name">The extension name.</param>
/// <param name="version">The requested version, or <see langword="null"/> to accept whatever the provider installs.</param>
[DebuggerDisplay("{Name,nq} (extension)")]
public sealed class Extension(SqlIdentifier name, string? version = null) : DatabaseObject(name), IEquatable<Extension>
{
    /// <inheritdoc/>
    public override ObjectKind Kind => ObjectKind.Extension;

    /// <summary>
    /// The requested version, or <see langword="null"/> to accept whatever the provider installs.
    /// </summary>
    public string? Version { get; set; } = version;

    /// <inheritdoc/>
    public override Extension Clone() => new(Name, Version) { Comment = Comment };

    /// <summary>
    /// Structural equality over the declared definition; the comment is excluded.
    /// </summary>
    public bool Equals(Extension? other) =>
        other is not null
        && Name == other.Name
        && Version == other.Version;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Extension other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Name, Version);
}
