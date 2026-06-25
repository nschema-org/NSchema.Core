using NSchema.Operations;
using NSchema.Operations.Doctor;
using NSchema.Schema;
using NSchema.Schema.Model;
using NSchema.Schema.Model.Schemas;
using NSchema.State;
using NSchema.State.Model;
using NSchema.Tests.Helpers;

namespace NSchema.Tests.Operations.Doctor;

public sealed class DoctorOperationTests
{
    private readonly RecordingReporter _reporter = new();
    private readonly ISchemaStateSerializer _serializer = new SchemaStateSerializer();
    private readonly RecordingStateLock _stateLock = new();

    private DoctorOperation BuildSut(ISchemaProvider? online = null, ISchemaStateStore? store = null) =>
        new(_reporter, _serializer, _stateLock, online, store);

    private Task Run(DoctorOperation sut) => sut.Execute(new DoctorArguments(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Execute_WhenNothingConfigured_ReportsNeutralAndPasses()
    {
        // Arrange
        var sut = BuildSut(online: null, store: null);

        // Act
        await Run(sut);

        // Assert
        _reporter.Messages.ShouldContain((MessageKind.Announcement, "Database: not configured (offline mode)."));
        _reporter.Messages.ShouldContain((MessageKind.Announcement, "State store: not configured (offline planning unavailable)."));
        _reporter.Messages.ShouldContain((MessageKind.Success, "All checks passed."));
    }

    [Fact]
    public async Task Execute_WhenNoStoreConfigured_DoesNotProbeTheLock()
    {
        // Arrange
        var sut = BuildSut(online: null, store: null);

        // Act
        await Run(sut);

        // Assert — the no-op lock would always read as free, so probing it without a backend would be misleading.
        _stateLock.Peeks.ShouldBe(0);
    }

    [Fact]
    public async Task Execute_WhenDatabaseReachable_ReportsConnectedWithSchemaCount()
    {
        // Arrange
        var schema = new DatabaseSchema(Schemas: [new SchemaDefinition("app"), new SchemaDefinition("billing")]);
        var sut = BuildSut(online: new InMemorySchemaProvider(schema));

        // Act
        await Run(sut);

        // Assert
        _reporter.Messages.ShouldContain((MessageKind.Success, "Database: connected (2 schemas visible)."));
        _reporter.Messages.ShouldContain((MessageKind.Success, "All checks passed."));
    }

    [Fact]
    public async Task Execute_WhenDatabaseUnreachable_ReportsAndThrows()
    {
        // Arrange
        var sut = BuildSut(online: new ThrowingSchemaProvider(new InvalidOperationException("connection refused")));

        // Act / Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(sut));
        ex.Message.ShouldContain("1 problem");
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("Database: unreachable") && m.Message.Contains("connection refused"));
        _reporter.Infos.ShouldNotContain("All checks passed.");
    }

    [Fact]
    public async Task Execute_WhenStateStoreEmpty_ReportsBootstrap()
    {
        // Arrange — a store with nothing written yet (bootstrap).
        var sut = BuildSut(store: new RecordingStateStore());

        // Act
        await Run(sut);

        // Assert
        _reporter.Messages.ShouldContain((MessageKind.Success, "State store: reachable (no state recorded yet)."));
    }

    [Fact]
    public async Task Execute_WhenStateStoreHasValidSnapshot_ReportsValid()
    {
        // Arrange
        var store = new RecordingStateStore();
        await store.Write(_serializer.Serialize(new DatabaseSchema(Schemas: [new SchemaDefinition("app")])), TestContext.Current.CancellationToken);
        var sut = BuildSut(store: store);

        // Act
        await Run(sut);

        // Assert
        _reporter.Messages.ShouldContain((MessageKind.Success, "State store: reachable, recorded state is valid."));
    }

    [Fact]
    public async Task Execute_WhenStateStoreUnreachable_ReportsAndThrows()
    {
        // Arrange
        var sut = BuildSut(store: new ThrowingStateStore(new IOException("bucket not found")));

        // Act / Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(sut));
        ex.Message.ShouldContain("1 problem");
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("State store: unreachable") && m.Message.Contains("bucket not found"));
    }

    [Fact]
    public async Task Execute_WhenRecordedStateCorrupt_ReportsUnreadableAndThrows()
    {
        // Arrange — a payload the serializer cannot deserialize.
        var store = new ContentStateStore(new byte[] { 0x00, 0x01, 0x02 });
        var sut = BuildSut(store: store);

        // Act / Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(sut));
        ex.Message.ShouldContain("1 problem");
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("recorded state is unreadable"));
    }

    [Fact]
    public async Task Execute_WhenStoreConfigured_ReadsTheLockWithoutAcquiringIt()
    {
        // Arrange
        var sut = BuildSut(store: new RecordingStateStore());

        // Act
        await Run(sut);

        // Assert — a read-only peek: the lock is read, never acquired (which would momentarily contend).
        _stateLock.Peeks.ShouldBe(1);
        _stateLock.Acquisitions.ShouldBeEmpty();
        _reporter.Messages.ShouldContain((MessageKind.Success, "State lock: free."));
    }

    [Fact]
    public async Task Execute_WhenLockHeld_ReportsHolderButDoesNotFail()
    {
        // Arrange
        _stateLock.PeekResult = new StateLockInfo("id", "apply", "tom@dev", DateTimeOffset.UnixEpoch);
        var sut = BuildSut(store: new RecordingStateStore());

        // Act — a held lock is a state, not a misconfiguration, so doctor still passes.
        await Run(sut);

        // Assert
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("State lock: held by") && m.Message.Contains("tom@dev") && m.Message.Contains("apply"));
        _reporter.Messages.ShouldContain((MessageKind.Success, "All checks passed."));
    }

    [Fact]
    public async Task Execute_WhenMultipleChecksFail_TalliesAllOfThem()
    {
        // Arrange
        var sut = BuildSut(
            online: new ThrowingSchemaProvider(new InvalidOperationException("db down")),
            store: new ThrowingStateStore(new IOException("store down")));

        // Act / Assert
        var ex = await Should.ThrowAsync<InvalidOperationException>(() => Run(sut));
        ex.Message.ShouldContain("2 problems");
    }

    private sealed class ThrowingSchemaProvider(Exception exception) : ISchemaProvider
    {
        public ValueTask<DatabaseSchema> GetSchema(string[]? schemaNames = null, CancellationToken cancellationToken = default) =>
            throw exception;
    }

    private sealed class ThrowingStateStore(Exception exception) : ISchemaStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) => throw exception;
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => throw exception;
    }

    private sealed class ContentStateStore(byte[] content) : ISchemaStateStore
    {
        public Task<ReadOnlyMemory<byte>?> Read(CancellationToken cancellationToken = default) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(content);
        public Task Write(ReadOnlyMemory<byte> state, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
