using NSchema.Operations.Apply;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// End-to-end wiring: a real <see cref="NSchemaApplication"/> with no database provider must still resolve the
/// apply operation from the container (the default executor is registered unconditionally, with no data source)
/// and fail with a clear domain error rather than an opaque DI resolution exception.
/// </summary>
public sealed class OfflineApplyWiringTests
{
    [Fact]
    public async Task Apply_WithNoProviderConfigured_ThrowsClearError()
    {
        using var app = NSchemaApplication.CreateBuilder().Build();

        var ex = await Should.ThrowAsync<InvalidOperationException>(() => app.Apply(new ApplyArguments(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("requires a database provider");
    }
}
