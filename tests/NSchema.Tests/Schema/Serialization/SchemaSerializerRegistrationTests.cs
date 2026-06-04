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

        public Task Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
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
        var resolver = Resolve(b => b.AddSchemaSerializer<JsonSchemaDocumentSerializer>());

        // Registering a second JSON serializer keeps json resolvable and doesn't duplicate the format.
        resolver.ForFormat("json").ShouldBeOfType<JsonSchemaDocumentSerializer>();
        resolver.AvailableFormats.ShouldBe(["json"]);
    }

    [Fact]
    public void AddSchemaSerializer_OverridingFormat_LastWins()
    {
        var replacement = new StubSerializer("json");

        var resolver = Resolve(b => b.AddSchemaSerializer(replacement));

        // The user's later registration shadows the built-in JSON serializer.
        resolver.ForFormat("json").ShouldBeSameAs(replacement);
    }
}
