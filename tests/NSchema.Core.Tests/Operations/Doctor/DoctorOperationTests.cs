using NSchema.Diagnostics;
using NSchema.Operations.Progress;
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
    private readonly IProgress<OperationProgress> _progress = Substitute.For<IProgress<OperationProgress>>();
    private readonly ISchemaStateSerializer _serializer = new SchemaStateSerializer();
    private readonly RecordingStateLock _stateLock = new();

    private DoctorOperation BuildSut(ISchemaProvider? online = null, ISchemaStateStore? store = null, IStateLock? stateLock = null) =>
        new(_progress, _serializer, online, store, stateLock);

    private Task<Result> Run(DoctorOperation sut) => sut.Execute(new DoctorArguments(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Run_WhenNothingConfigured_ReportsNeutralAndPasses()
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
    }

    [Fact]
    public async Task Run_WhenNoLockConfigured_DoesNotProbeTheLock()
    {
        // Arrange — no backend provides a lock, so there is nothing to probe (stateLock is not wired).
        var sut = BuildSut(online: null, store: null, stateLock: null);

        // Act
        var result = await Run(sut);

        // Assert
        _stateLock.Peeks.ShouldBe(0);
        result.Diagnostics.ShouldNotContain(d => d.Message.StartsWith("State lock:"));
    }

    [Fact]
    public async Task Run_WhenDatabaseReachable_ReportsConnectedWithSchemaCount()
    {
        // Arrange
        var schema = new DatabaseSchema(Schemas: [new SchemaDefinition("app"), new SchemaDefinition("billing")]);
        var sut = BuildSut(online: new InMemorySchemaProvider(schema));

        // Act
        var result = await Run(sut);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.Select(d => d.Message).ShouldContain("Database: connected (2 schemas visible).");
    }

    [Fact]
    public async Task Run_WhenDatabaseUnreachable_ReportsAndFails()
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
    }

    [Fact]
    public async Task Run_WhenStateStoreEmpty_ReportsBootstrap()
    {
        // Arrange — a store with nothing written yet (bootstrap).
        var sut = BuildSut(store: new RecordingStateStore());

        // Act
        var result = await Run(sut);

        // Assert
        result.Diagnostics.Select(d => d.Message).ShouldContain("State store: reachable (no state recorded yet).");
    }

    [Fact]
    public async Task Run_WhenStateStoreHasValidSnapshot_ReportsValid()
    {
        // Arrange
        var store = new RecordingStateStore();
        await store.Write(_serializer.Serialize(new DatabaseSchema(Schemas: [new SchemaDefinition("app")])), TestContext.Current.CancellationToken);
        var sut = BuildSut(store: store);

        // Act
        var result = await Run(sut);

        // Assert
        result.Diagnostics.Select(d => d.Message).ShouldContain("State store: reachable, recorded state is valid.");
    }

    [Fact]
    public async Task Run_WhenStateStoreUnreachable_ReportsAndFails()
    {
        // Arrange
        var sut = BuildSut(store: new ThrowingStateStore(new IOException("bucket not found")));

        // Act
        var result = await Run(sut);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldSatisfyAllConditions(
            m => m.ShouldContain("State store: unreachable"),
            m => m.ShouldContain("bucket not found"));
    }

    [Fact]
    public async Task Run_WhenRecordedStateCorrupt_ReportsUnreadableAndFails()
    {
        // Arrange — a payload the serializer cannot deserialize.
        var store = new ContentStateStore(new byte[] { 0x00, 0x01, 0x02 });
        var sut = BuildSut(store: store);

        // Act
        var result = await Run(sut);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Errors.ShouldHaveSingleItem().Message.ShouldContain("recorded state is unreadable");
    }

    [Fact]
    public async Task Run_WhenStoreConfigured_ReadsTheLockWithoutAcquiringIt()
    {
        // Arrange
        var sut = BuildSut(store: new RecordingStateStore(), stateLock: _stateLock);

        // Act
        var result = await Run(sut);

        // Assert — a read-only peek: the lock is read, never acquired (which would momentarily contend).
        _stateLock.Peeks.ShouldBe(1);
        _stateLock.Acquisitions.ShouldBeEmpty();
        result.Diagnostics.Select(d => d.Message).ShouldContain("State lock: free.");
    }

    [Fact]
    public async Task Run_WhenLockHeld_ReportsHolderButDoesNotFail()
    {
        // Arrange
        _stateLock.PeekResult = new StateLockInfo("id", "apply", "tom@dev", DateTimeOffset.UnixEpoch);
        var sut = BuildSut(store: new RecordingStateStore(), stateLock: _stateLock);

        // Act — a held lock is a state, not a misconfiguration, so doctor still passes.
        var result = await Run(sut);

        // Assert — surfaced as a warning diagnostic, but the result is still a success.
        result.IsSuccess.ShouldBeTrue();
        result.Diagnostics.ShouldContain(d => d.Severity == DiagnosticSeverity.Warning
            && d.Message.Contains("State lock: held by") && d.Message.Contains("tom@dev") && d.Message.Contains("apply"));
    }

    [Fact]
    public async Task Run_WhenMultipleChecksFail_AggregatesAllOfThem()
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
