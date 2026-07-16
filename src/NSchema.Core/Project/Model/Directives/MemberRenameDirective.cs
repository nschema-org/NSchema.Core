using NSchema.Model;
namespace NSchema.Project.Domain.Models;

/// <summary>
/// A table-member rename directive.
/// </summary>
/// <remarks>Renames never move a member across objects, so the target is a bare name.</remarks>
/// <param name="From">The member's current address.</param>
/// <param name="To">The declared name the member is renamed to.</param>
public sealed record MemberRenameDirective(MemberReference From, SqlIdentifier To);
