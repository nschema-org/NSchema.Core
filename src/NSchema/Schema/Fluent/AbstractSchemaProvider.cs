using NSchema.Migration;

namespace NSchema.Schema.Fluent;

public abstract class AbstractSchemaProvider : IDesiredSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<string> _droppedSchemas = [];

    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    public void DropSchema(string name) => _droppedSchemas.Add(name);

    public Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default)
    {
        var schemas = _schemas.Select(s => s.Build()).ToList();
        return Task.FromResult(new DatabaseSchema(schemas, _droppedSchemas));
    }
}
