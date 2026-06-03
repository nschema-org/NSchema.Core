using System.Reflection;
using NSchema.Schema.Model;

namespace NSchema.Scripts;

/// <summary>
/// Provides migration scripts by loading a single embedded resource from a specified assembly that matches a given resource name.
/// </summary>
/// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
/// <param name="assembly">The assembly containing the embedded resource to be loaded as a migration script.</param>
/// <param name="resourceName">The name of the embedded resource in the assembly to be loaded as a migration script.</param>
/// <param name="name">An optional name to assign to the script; if not provided, a name will be derived from the resource name.</param>
internal sealed class EmbeddedResourceScriptProvider(ScriptType type, Assembly assembly, string resourceName, string? name = null) : IScriptProvider
{
    public async Task<IReadOnlyList<Script>> GetScripts(CancellationToken cancellationToken = default)
    {
        var sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
        return [new Script(name ?? EmbeddedResource.DeriveName(resourceName), sql, type)];
    }
}
