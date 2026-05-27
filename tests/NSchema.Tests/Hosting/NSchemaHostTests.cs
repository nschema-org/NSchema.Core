using Microsoft.Extensions.Hosting;
using NSchema.Hosting;

namespace NSchema.Tests.Hosting;

public sealed class NSchemaHostTests
{
    [Fact]
    public async Task Execute_StopsApplication_OnSuccess()
    {
        var pipeline = Substitute.For<IMigrationPipeline>();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = new NSchemaHost(lifetime, pipeline);

        await sut.StartAsync(CancellationToken.None);
        await sut.ExecuteTask!;

        await pipeline.Received(1).Run(Arg.Any<CancellationToken>());
        lifetime.Received(1).StopApplication();
    }

    [Fact]
    public async Task Execute_StopsApplication_WhenPipelineThrows()
    {
        var pipeline = Substitute.For<IMigrationPipeline>();
        pipeline.Run(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("boom"));
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        var sut = new NSchemaHost(lifetime, pipeline);

        await sut.StartAsync(CancellationToken.None);
        await Should.ThrowAsync<InvalidOperationException>(async () => await sut.ExecuteTask!);

        lifetime.Received(1).StopApplication();
    }
}
