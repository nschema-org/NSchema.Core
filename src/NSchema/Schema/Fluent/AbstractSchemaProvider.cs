using NSchema.Migration;

namespace NSchema.Schema.Fluent;

/// <summary>
/// Provides an abstract base class for defining a desired database schema using a fluent interface.
/// </summary>
public abstract class AbstractSchemaProvider : IDesiredSchemaProvider
{
    private readonly List<SchemaBuilder> _schemas = [];
    private readonly List<string> _droppedSchemas = [];

    /// <summary>
    /// Adds a new schema with the specified name to the desired database schema and returns a builder for configuring it.
    /// </summary>
    /// <param name="name">The name of the schema to define.</param>
    /// <returns>A <see cref="SchemaBuilder"/> instance that can be used to configure the schema.</returns>
    public SchemaBuilder Schema(string name)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        return builder;
    }

    /// <summary>
    /// Adds a new schema with the specified name to the desired database schema and returns a builder for configuring it.
    /// </summary>
    /// <param name="name">The name of the schema to define.</param>
    /// <param name="configure">A delegate that can be used to configure the schema.</param>
    /// <returns>The current schema provider so that calls can be chained.</returns>
    public AbstractSchemaProvider Schema(string name, Action<SchemaBuilder> configure)
    {
        var builder = new SchemaBuilder(name);
        _schemas.Add(builder);
        configure.Invoke(builder);
        return this;
    }

    /// <summary>
    /// Marks the schema with the specified name for dropping. This indicates that the schema should be removed from the database when the migration is applied.
    /// </summary>
    /// <param name="name">The name of the schema to drop.</param>
    /// <returns>The current schema provider so that calls can be chained.</returns>
    public AbstractSchemaProvider DropSchema(string name)
    {
         _droppedSchemas.Add(name);
         return this;
    }

    /// <inheritdoc/>
    public Task<DatabaseSchema> GetSchema(CancellationToken cancellationToken = default)
    {
        var schemas = _schemas.Select(s => s.Build()).ToList();
        return Task.FromResult(new DatabaseSchema(schemas, _droppedSchemas));
    }
}
