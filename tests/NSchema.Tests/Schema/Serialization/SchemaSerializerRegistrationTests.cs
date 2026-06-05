using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Schema.Serialization;

public sealed class SchemaSerializerRegistrationTests
{
    /// <summary>A no-op serializer that only carries a format, for registration tests.</summary>
    private sealed class StubSerializer(string format) : ISchemaDocumentSerializer
    {
        public string Format => format;

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
    public void AddSchemaSerializer_Instance_IsResolvable()
    {
        var yaml = new StubSerializer("yaml");

        var resolver = Resolve(b => b.AddSchemaSerializer(yaml));

        resolver.ForFormat("yaml").ShouldBeSameAs(yaml);
        resolver.AvailableFormats.ShouldBe(["json", "yaml"], ignoreOrder: true);
    }

    [Fact]
    public void AddSchemaSerializer_Generic_IsResolvable()
    {
        // Re-registering the same implementation type is deduplicated by TryAddEnumerable, so json stays
        // resolvable without a duplicate-format conflict.
        var resolver = Resolve(b => b.AddSchemaSerializer<JsonSchemaDocumentSerializer>());

        resolver.ForFormat("json").ShouldBeOfType<JsonSchemaDocumentSerializer>();
        resolver.AvailableFormats.ShouldBe(["json"]);
    }

    [Fact]
    public void AddSchemaSerializer_DuplicateFormat_Throws()
    {
        // A different serializer for the built-in 'json' format is ambiguous and throws when resolved.
        Should.Throw<InvalidOperationException>(
            () => Resolve(b => b.AddSchemaSerializer(new StubSerializer("json"))));
    }
}
