using NSchema.Migration;

namespace NSchema.Schema.Fluent;

public abstract class AbstractSchemaProvider : IDesiredSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<Script> _preScripts = [];
    private readonly List<Script> _postScripts = [];
    private readonly List<string> _droppedSchemas = [];

    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    public void DropSchema(string name) => _droppedSchemas.Add(name);

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
        var schemas = _schemas.Select(s => s.Build()).ToList();
        return Task.FromResult(new DatabaseSchema(
            schemas, _preScripts, _postScripts,
            _droppedSchemas.Count > 0 ? _droppedSchemas : null));
    }
}
