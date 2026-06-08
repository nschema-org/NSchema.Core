using Microsoft.Extensions.DependencyInjection;
using NSchema.Import;
using NSchema.Resolution;
using NSchema.Schema.Model;

namespace NSchema.Tests.Import;

public sealed class ImportTargetRegistrationTests
{
    private sealed class StubImportTarget : ISchemaImportTarget
    {
        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class OtherStubImportTarget : ISchemaImportTarget
    {
        public Task Write(DatabaseSchema schema, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private static IKeyedResolver<ISchemaImportTarget> Resolve(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services.GetRequiredService<IKeyedResolver<ISchemaImportTarget>>();
    }

    [Fact]
    public void Default_HasNoCurrentTarget()
    {
        var resolver = Resolve(_ => { });

        resolver.HasCurrent.ShouldBeFalse();
        Should.Throw<InvalidOperationException>(() => resolver.Current);
    }

    [Fact]
    public void AddImportTarget_IsResolvableByKey()
    {
        var resolver = Resolve(b => b.AddImportTarget<StubImportTarget>("stub"));

        resolver.Resolve("stub").ShouldBeOfType<StubImportTarget>();
    }

    [Fact]
    public void AddImportTarget_DuplicateKey_ReplacesPrevious()
    {
        var resolver = Resolve(b => b
            .AddImportTarget<StubImportTarget>("stub")
            .AddImportTarget<OtherStubImportTarget>("stub"));

        resolver.Resolve("stub").ShouldBeOfType<OtherStubImportTarget>();
    }

    [Fact]
    public void AddImportTarget_SetsDefaultTarget_OnFirstRegistration()
    {
        var resolver = Resolve(b => b.AddImportTarget<StubImportTarget>("stub"));

        resolver.HasCurrent.ShouldBeTrue();
        resolver.Current.ShouldBeOfType<StubImportTarget>();
    }

    [Fact]
    public void AddImportTarget_DoesNotOverrideDefaultTarget_WhenAlreadySet()
    {
        var resolver = Resolve(b => b
            .AddImportTarget<StubImportTarget>("first")
            .AddImportTarget<OtherStubImportTarget>("second"));

        resolver.Current.ShouldBeOfType<StubImportTarget>();
    }

    [Fact]
    public void AddFileImportTarget_RegistersFileTarget()
    {
        var resolver = Resolve(b => b.AddFileImportTarget(_ => { }));

        resolver.HasCurrent.ShouldBeTrue();
        resolver.Current.ShouldBeOfType<FileSchemaImportTarget>();
    }
}
