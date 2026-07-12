using NSchema.Project.Domain.Models;
namespace NSchema.Project;

/// <summary>
/// Provides the resolved project that represents the desired state of the database.
/// </summary>
public interface IProjectProvider
{
    /// <summary>
    /// Resolves the project, optionally scoped to a set of schema names.
    /// </summary>
    /// <param name="schemaNames">The schemas to scope to, or <see langword="null"/> for the whole project.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>The resolved project, with any non-fatal findings raised while reading.</returns>
    ValueTask<Result<ProjectDefinition>> GetProject(string[]? schemaNames = null, CancellationToken cancellationToken = default);
}
