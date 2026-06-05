using System.Reflection;
using NSchema.Scripts.Model;

namespace NSchema.Scripts;

/// <summary>
/// Provides migration scripts by loading embedded resources from a specified assembly that match a given resource name prefix.
/// </summary>
/// <param name="type">The type of the script, indicating when it should be executed in relation to the main migration actions.</param>
/// <param name="assembly">The assembly containing the embedded resources to be loaded as migration scripts.</param>
/// <param name="resourcePrefix">The prefix used to filter embedded resources in the assembly.</param>
internal sealed class EmbeddedResourcePrefixScriptProvider(ScriptType type, Assembly assembly, string resourcePrefix) : IScriptProvider
{
    public async ValueTask<IReadOnlyList<Script>> GetScripts(CancellationToken cancellationToken = default)
    {
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        var scripts = new List<Script>();
        foreach (var resourceName in resourceNames)
        {
            var sql = await EmbeddedResource.Read(assembly, resourceName, cancellationToken);
            scripts.Add(new Script(EmbeddedResource.DeriveName(resourceName), sql, type));
        }
        return scripts;
    }
}
