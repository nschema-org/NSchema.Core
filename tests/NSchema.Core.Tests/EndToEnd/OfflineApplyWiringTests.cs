using NSchema.Operations.Apply;

namespace NSchema.Tests.EndToEnd;

/// <summary>
/// End-to-end wiring: a real <see cref="NSchemaApplication"/> with no database provider must still resolve the
/// apply operation from the container (the default executor is registered unconditionally, with no data source)
/// and fail with a clear domain diagnostic rather than an opaque DI resolution exception.
/// </summary>
public sealed class OfflineApplyWiringTests
{
    [Fact]
    public async Task Apply_WithNoProviderConfigured_FailsWithClearError()
    {
        using var app = NSchemaApplication.CreateBuilder().Build();

        var result = await app.Apply(new ApplyArguments(), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("requires a database provider");
    }
}
