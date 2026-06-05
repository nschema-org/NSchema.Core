using NSchema.Schema.Model;
using NSchema.Schema.Serialization;

namespace NSchema.Tests.Schema.Serialization;

public sealed class DefaultSchemaDocumentSerializerResolverTests
{
    /// <summary>A no-op serializer that only carries a format, for resolution tests.</summary>
    private sealed class StubSerializer(string format) : ISchemaDocumentSerializer
    {
        public string Format => format;

        public Task Write(DatabaseSchema schema, Stream destination, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<DatabaseSchema> Read(Stream source, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static DefaultSchemaDocumentSerializerResolver Resolver(params ISchemaDocumentSerializer[] serializers)
        => new(serializers);

    [Fact]
    public void ForFormat_ReturnsRegisteredSerializer()
    {
        var json = new StubSerializer("json");
        var sut = Resolver(json, new StubSerializer("yaml"));

        sut.Resolve("json").ShouldBeSameAs(json);
    }

    [Fact]
    public void ForFormat_IsCaseInsensitive()
    {
        var yaml = new StubSerializer("yaml");
        var sut = Resolver(yaml);

        sut.Resolve("YAML").ShouldBeSameAs(yaml);
    }

    [Fact]
    public void Constructor_Throws_OnDuplicateFormat()
    {
        var ex = Should.Throw<InvalidOperationException>(
            () => Resolver(new StubSerializer("json"), new StubSerializer("JSON")));

        ex.Message.ShouldContain("json");
    }

    [Fact]
    public void ForFormat_UnknownFormat_ThrowsWithAvailableFormats()
    {
        var sut = Resolver(new StubSerializer("json"), new StubSerializer("yaml"));

        var ex = Should.Throw<InvalidOperationException>(() => sut.Resolve("xml"));

        ex.Message.ShouldContain("xml");
        ex.Message.ShouldContain("json");
        ex.Message.ShouldContain("yaml");
    }

    [Fact]
    public void ForFormat_NoSerializers_ThrowsWithNone()
    {
        var sut = Resolver();

        var ex = Should.Throw<InvalidOperationException>(() => sut.Resolve("json"));

        ex.Message.ShouldContain("none");
    }

    [Theory]
    [InlineData(null)]
    public void ForFormat_RejectsMissingFormat(string? format)
        => Should.Throw<ArgumentException>(() => Resolver(new StubSerializer("json")).ForFormat(format!));

    [Fact]
    public void TryForFormat_ReturnsFalse_WhenUnregistered()
    {
        var sut = Resolver(new StubSerializer("json"));

        sut.TryResolve("yaml", out var serializer).ShouldBeFalse();
        serializer.ShouldBeNull();
    }

    [Fact]
    public void TryForFormat_ReturnsTrueAndSerializer_WhenRegistered()
    {
        var json = new StubSerializer("json");
        var sut = Resolver(json);

        sut.TryResolve("json", out var serializer).ShouldBeTrue();
        serializer.ShouldBeSameAs(json);
    }

    [Fact]
    public void AvailableFormats_ListsRegisteredFormats()
    {
        var sut = Resolver(new StubSerializer("json"), new StubSerializer("yaml"));

        sut.Keys.ShouldBe(["json", "yaml"], ignoreOrder: true);
    }

    [Fact]
    public void AvailableFormats_Empty_WhenNoSerializers()
        => Resolver().Keys.ShouldBeEmpty();
}
