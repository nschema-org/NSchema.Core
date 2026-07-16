using NSchema.Model;
namespace NSchema.Plan.Domain.Models.Domains;

/// <summary>
/// Represents setting, changing, or clearing the comment on a domain.
/// </summary>
/// <param name="SchemaName">The name of the schema containing the domain.</param>
/// <param name="DomainName">The name of the domain.</param>
/// <param name="OldComment">The previous comment, if any.</param>
/// <param name="NewComment">The new comment, if any.</param>
public sealed record SetDomainComment(SqlIdentifier SchemaName, SqlIdentifier DomainName, string? OldComment, string? NewComment) : MigrationAction
{
    /// <inheritdoc />
    public override bool IsDestructive => false;
}
