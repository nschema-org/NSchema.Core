using NSchema.Schema;

namespace NSchema.Migration;

internal sealed class InlineScriptProvider(IReadOnlyList<Script> pre, IReadOnlyList<Script> post) : IDeploymentScriptProvider
{
    public IReadOnlyList<Script> PreDeploymentScripts => pre;
    public IReadOnlyList<Script> PostDeploymentScripts => post;
}
