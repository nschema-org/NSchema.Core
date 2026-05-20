using NSchema.Domain.Schema;

namespace NSchema.Fluent;

public sealed class DatabaseModelBuilder
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<DeploymentScript> _preScripts = [];
    private readonly List<DeploymentScript> _postScripts = [];

    public DatabaseModelBuilder Schema(string name, Action<SchemaBuilder> configure)
    {
        var builder = new SchemaBuilder(name);
        configure(builder);
        _schemas.Add(builder);
        return this;
    }

    public DatabaseModelBuilder PreDeploymentScript(string name, string sql)
    {
        _preScripts.Add(new DeploymentScript(name, sql));
        return this;
    }

    public DatabaseModelBuilder PostDeploymentScript(string name, string sql)
    {
        _postScripts.Add(new DeploymentScript(name, sql));
        return this;
    }

    public DatabaseModel Build() =>
        new(_schemas.Select(s => s.Build()).ToList(),
            _preScripts.Count > 0 ? _preScripts : null,
            _postScripts.Count > 0 ? _postScripts : null);
}
