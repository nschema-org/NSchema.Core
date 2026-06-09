using Microsoft.Extensions.DependencyInjection;
using NSchema.Resolution;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;
using NSchema.Schema.Serialization.Ddl;

namespace NSchema.Tests.Schema.Serialization;

public sealed class SchemaSerializerRegistrationTests
{
    /// <summary>A no-op serializer for the 'yaml' format, for registration tests.</summary>
    private sealed class YamlStubSerializer : ISchemaSerializer
    {
        public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>A no-op serializer that claims the built-in 'json' format, for override tests.</summary>
    private sealed class JsonStubSerializer : ISchemaSerializer
    {
        public string Format => "json";

        public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static IKeyedResolver<ISchemaSerializer> Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetRequiredService<IKeyedResolver<ISchemaSerializer>>();
    }

    [Fact]
    public void Default_RegistersJsonSerializer()
    {
        var resolver = Resolve(_ => { });

        resolver.Resolve("json").ShouldBeOfType<JsonSchemaSerializer>();
    }

    [Fact]
    public void Default_RegistersDdlSerializer()
    {
        var resolver = Resolve(_ => { });

        resolver.Resolve("sql").ShouldBeOfType<DdlSchemaSerializer>();
    }

    [Fact]
    public void AddSchemaSerializer_RegistersResolvableSerializer()
    {
        var resolver = Resolve(b => b.AddSchemaSerializer<YamlStubSerializer>("yaml"));

        resolver.Resolve("yaml").ShouldBeOfType<YamlStubSerializer>();
        resolver.Resolve("json").ShouldBeOfType<JsonSchemaSerializer>();
    }

    [Fact]
    public void AddSchemaSerializer_DuplicateFormat_KeepsFirst()
    {
        var resolver = Resolve(b => b.AddSchemaSerializer<JsonStubSerializer>("json"));

        resolver.Resolve("json").ShouldBeOfType<JsonSchemaSerializer>();
    }

    [Fact]
    public void UseSchemaSerializer_OverridesBuiltIn_ForSameFormat()
    {
        var resolver = Resolve(b => b.UseSchemaSerializer<JsonStubSerializer>("json"));

        resolver.Resolve("json").ShouldBeOfType<JsonStubSerializer>();
    }
}
