using NSchema.Domain.Schema;

namespace NSchema.Target.Fluent;

public sealed class SchemaBuilder
{
    private readonly string _name;
    private readonly List<TableBuilder> _tables = [];
    private string? _previousName;

    internal SchemaBuilder(string name) => _name = name;

    public SchemaBuilder Table(string name, Action<TableBuilder> configure)
    {
        var builder = new TableBuilder(name);
        configure(builder);
        _tables.Add(builder);
        return this;
    }

    public SchemaBuilder WasPreviouslyNamed(string previousName) { _previousName = previousName; return this; }

    internal Schema Build() =>
        new(_name, _tables.Select(t => t.Build()).ToList(), _previousName);
}
