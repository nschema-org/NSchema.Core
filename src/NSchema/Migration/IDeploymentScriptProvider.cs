using NSchema.Schema;

namespace NSchema.Migration;

public interface IDeploymentScriptProvider
{
    IReadOnlyList<Script> PreDeploymentScripts { get; }
    IReadOnlyList<Script> PostDeploymentScripts { get; }
}
