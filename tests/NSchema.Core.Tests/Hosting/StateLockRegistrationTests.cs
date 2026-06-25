using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSchema.State;
using NSchema.State.File;
using NSchema.State.Model;

namespace NSchema.Tests.Hosting;

public sealed class StateLockRegistrationTests
{
    private static IServiceProvider Build(Action<NSchemaApplicationBuilder> configure)
    {
        var builder = NSchemaApplication.CreateBuilder();
        configure(builder);
        return builder.Build().Services;
    }

    [Fact]
    public void NoStore_DefaultsToNoOpLock()
    {
        var services = Build(_ => { });

        services.GetRequiredService<IStateLock>().ShouldBeOfType<NoOpStateLock>();
    }

    [Fact]
    public void FileStateStore_AutoRegistersAFileLockAtDerivedPath()
    {
        var services = Build(b => b.UseFileStateStore("state/schema.json"));

        services.GetRequiredService<IStateLock>().ShouldBeOfType<FileStateLock>();
        services.GetRequiredService<IOptions<FileStateLockOptions>>().Value.Path.ShouldBe("state/schema.json.lock");
    }

    [Fact]
    public void StoreThatAlsoLocks_RegistersTheSameInstanceForBothSeams()
    {
        var services = Build(b => b.UseStateStore<LockingStore>());

        var store = services.GetRequiredService<ISchemaStateStore>();
        var stateLock = services.GetRequiredService<IStateLock>();

        stateLock.ShouldBeSameAs(store);
    }

    [Fact]
    public void StoreThatDoesNotLock_LeavesLockingOff()
    {
        var services = Build(b => b.UseStateStore<StoreOnly>());

        services.GetRequiredService<IStateLock>().ShouldBeOfType<NoOpStateLock>();
    }

    [Fact]
    public void ExplicitLockBeforeStore_Wins()
    {
        var services = Build(b => b.UseStateLock<CustomLock>().UseFileStateStore("schema.json"));

        services.GetRequiredService<IStateLock>().ShouldBeOfType<CustomLock>();
    }

    [Fact]
    public void ExplicitLockAfterStore_Wins()
    {
        var services = Build(b => b.UseFileStateStore("schema.json").UseStateLock<CustomLock>());

        services.GetRequiredService<IStateLock>().ShouldBeOfType<CustomLock>();
    }

    private sealed class StoreOnly : ISchemaStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class LockingStore : ISchemaStateStore, IStateLock
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) => Task.FromResult<ReadOnlyMemory<byte>?>(null);
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StateLockInfo?> ForceUnlock(CancellationToken cancellationToken = default) => Task.FromResult<StateLockInfo?>(null);
    }

    // Implements IStateLock *without* Peek, exercising that the new member's default implementation keeps existing
    // implementers source-compatible (no breaking change).
    private sealed class CustomLock : IStateLock
    {
        public Task<IStateLockHandle> Acquire(StateLockRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<StateLockInfo?> ForceUnlock(CancellationToken cancellationToken = default) => Task.FromResult<StateLockInfo?>(null);
    }
}
