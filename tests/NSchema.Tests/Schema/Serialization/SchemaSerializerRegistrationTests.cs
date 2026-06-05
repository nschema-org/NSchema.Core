using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Schema.Serialization;

public sealed class SchemaSerializerRegistrationTests
{
    /// <summary>A no-op serializer for the 'yaml' format, for registration tests.</summary>
    private sealed class YamlStubSerializer : ISchemaDocumentSerializer
    {
        public string Format => "yaml";

        public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    /// <summary>A no-op serializer that claims the built-in 'json' format, for override tests.</summary>
    private sealed class JsonStubSerializer : ISchemaDocumentSerializer
    {
        public string Format => "json";

        public ValueTask Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static ISchemaDocumentSerializerResolver Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        using var app = builder.Build();
        return app.Services.GetRequiredService<ISchemaDocumentSerializerResolver>();
    }

    [Fact]
    public void Default_RegistersJsonSerializer()
    {
        var resolver = Resolve(_ => { });

        resolver.ForFormat("json").ShouldBeOfType<JsonSchemaDocumentSerializer>();
        resolver.AvailableFormats.ShouldContain("json");
    }

    [Fact]
    public void AddSchemaSerializer_RegistersResolvableSerializer()
    {
        var resolver = Resolve(b => b.AddSchemaSerializer<YamlStubSerializer>());

        resolver.ForFormat("yaml").ShouldBeOfType<YamlStubSerializer>();
        resolver.AvailableFormats.ShouldBe(["json", "yaml"], ignoreOrder: true);
    }

    [Fact]
    public void AddSchemaSerializer_SameType_IsDeduplicated()
    {
        // Re-registering the built-in implementation type is deduplicated by TryAddEnumerable, so json stays
        // resolvable without a duplicate registration.
        var resolver = Resolve(b => b.AddSchemaSerializer<JsonSchemaDocumentSerializer>());

        resolver.ForFormat("json").ShouldBeOfType<JsonSchemaDocumentSerializer>();
        resolver.AvailableFormats.ShouldBe(["json"]);
    }

    [Fact]
    public void AddSchemaSerializer_OverridesBuiltIn_ForSameFormat()
    {
        // The built-in json serializer is registered first, so a caller's serializer for the same format,
        // added afterwards, replaces it (last registration wins).
        var resolver = Resolve(b => b.AddSchemaSerializer<JsonStubSerializer>());

        resolver.ForFormat("json").ShouldBeOfType<JsonStubSerializer>();
    }
}
