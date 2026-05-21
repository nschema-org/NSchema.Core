using NSchema.Domain.Schema;

namespace NSchema.Target.Fluent;

public abstract class AbstractSchemaProvider : ITargetSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<Script> _preScripts = [];
    private readonly List<Script> _postScripts = [];

    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    public void PreDeploymentScript(string name, string sql)
    {
        _preScripts.Add(new Script(name, sql));
    }

    public void PostDeploymentScript(string name, string sql)
    {
        _postScripts.Add(new Script(name, sql));
    }

    public Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default)
    {
        var schema = new DatabaseSchema(
            _schemas.Select(s => s.Build()).ToList(),
            _preScripts.Count > 0 ? _preScripts : null,
            _postScripts.Count > 0 ? _postScripts : null
        );
        return Task.FromResult(schema);
    }
}
