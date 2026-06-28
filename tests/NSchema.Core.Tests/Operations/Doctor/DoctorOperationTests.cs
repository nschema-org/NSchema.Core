using NSchema.Diagnostics;
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

    private DoctorOperation BuildSut(ISchemaProvider? online = null, ISchemaStateStore? store = null, IStateLock? stateLock = null) =>
        new(_reporter, _serializer, online, store, stateLock);

    private Task<Result> Run(DoctorOperation sut) => sut.Execute(new DoctorArguments(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Execute_WhenNothingConfigured_ReportsNeutralAndPasses()
    {
        // Arrange
        var sut = BuildSut(online: null, store: null);

        // Act
        var result = await Run(sut);

        // Assert — the neutral findings are carried back, and with no errors the result is a success.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Message).ShouldBe(
        [
            "Database: not configured (offline mode).",
            "State store: not configured (offline planning unavailable).",
        ]);
        _reporter.Messages.ShouldContain((MessageKind.Announcement, "Database: not configured (offline mode)."));
        _reporter.Messages.ShouldContain((MessageKind.Announcement, "State store: not configured (offline planning unavailable)."));
        _reporter.Messages.ShouldContain((MessageKind.Success, "All checks passed."));
    }

    [Fact]
    public async Task Execute_WhenNoLockConfigured_DoesNotProbeTheLock()
    {
        // Arrange — no backend provides a lock, so there is nothing to probe (stateLock is not wired).
        var sut = BuildSut(online: null, store: null, stateLock: null);

        // Act
        await Run(sut);

        // Assert
        _stateLock.Peeks.ShouldBe(0);
        _reporter.Messages.ShouldNotContain(m => m.Message.StartsWith("State lock:"));
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
    public async Task Execute_WhenDatabaseUnreachable_ReportsAndFails()
    {
        // Arrange
        var sut = BuildSut(online: new ThrowingSchemaProvider(new InvalidOperationException("connection refused")));

        // Act
        var result = await Run(sut);

        // Assert — the failure is carried back as an error diagnostic, not thrown.
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldSatisfyAllConditions(
            m => m.ShouldContain("Database: unreachable"),
            m => m.ShouldContain("connection refused"));
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
    public async Task Execute_WhenStateStoreUnreachable_ReportsAndFails()
    {
        // Arrange
        var sut = BuildSut(store: new ThrowingStateStore(new IOException("bucket not found")));

        // Act
        var result = await Run(sut);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("State store: unreachable");
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("State store: unreachable") && m.Message.Contains("bucket not found"));
    }

    [Fact]
    public async Task Execute_WhenRecordedStateCorrupt_ReportsUnreadableAndFails()
    {
        // Arrange — a payload the serializer cannot deserialize.
        var store = new ContentStateStore(new byte[] { 0x00, 0x01, 0x02 });
        var sut = BuildSut(store: store);

        // Act
        var result = await Run(sut);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("recorded state is unreadable");
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("recorded state is unreadable"));
    }

    [Fact]
    public async Task Execute_WhenStoreConfigured_ReadsTheLockWithoutAcquiringIt()
    {
        // Arrange
        var sut = BuildSut(store: new RecordingStateStore(), stateLock: _stateLock);

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
        var sut = BuildSut(store: new RecordingStateStore(), stateLock: _stateLock);

        // Act — a held lock is a state, not a misconfiguration, so doctor still passes.
        var result = await Run(sut);

        // Assert — surfaced as a warning diagnostic, but the result is still a success.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning && d.Message.Contains("State lock: held by"));
        _reporter.Messages.ShouldContain(m => m.Kind == MessageKind.Warning && m.Message.Contains("State lock: held by") && m.Message.Contains("tom@dev") && m.Message.Contains("apply"));
        _reporter.Messages.ShouldContain((MessageKind.Success, "All checks passed."));
    }

    [Fact]
    public async Task Execute_WhenMultipleChecksFail_AggregatesAllOfThem()
    {
        // Arrange
        var sut = BuildSut(
            online: new ThrowingSchemaProvider(new InvalidOperationException("db down")),
            store: new ThrowingStateStore(new IOException("store down")));

        // Act — both failures are surfaced together in one result, not one-at-a-time.
        var result = await Run(sut);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.Count().ShouldBe(2);
        result.Errors.Select(e => e.Message).ShouldContain(m => m.Contains("db down"));
        result.Errors.Select(e => e.Message).ShouldContain(m => m.Contains("store down"));
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
