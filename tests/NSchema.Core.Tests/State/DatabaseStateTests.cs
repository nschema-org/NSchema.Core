using NSchema.State.Domain.Models;
using NSchema.Project.Domain.Models;
using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Tests.State;

public sealed class DatabaseStateTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordScripts_AddsEntriesToTheLedger()
    {
        // Arrange
        var state = DatabaseState.Empty;

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().ShouldBe(new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", _now));
    }

    [Fact]
    public void RecordScripts_ReplacesAnEarlierExecutionByName_CaseInsensitively()
    {
        // Arrange
        var state = new DatabaseState(new Database(), [new ScriptExecution(new ScriptReference(null, new SqlIdentifier("Seed")), "old", DateTimeOffset.UnixEpoch)]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "new", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().Hash.ShouldBe("new");
    }

    [Fact]
    public void RecordScripts_LeavesOtherEntriesAlone()
    {
        // Arrange
        var existing = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "hash", DateTimeOffset.UnixEpoch);
        var state = new DatabaseState(new Database(), [existing]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldBe([existing, new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", _now)]);
    }

    [Fact]
    public void RecordScripts_NothingExecuted_ReturnsTheSameState()
        => DatabaseState.Empty.RecordExecution([]).ShouldBeSameAs(DatabaseState.Empty);

    [Fact]
    public void FindScript_MatchesByName_CaseInsensitively()
    {
        // Arrange
        var existing = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("Seed")), "abc", _now);
        var state = new DatabaseState(new Database(), [existing]);

        // Act
        var found = state.FindExecution(new ScriptReference(null, new SqlIdentifier("seed")));

        // Assert
        found.ShouldBe(existing);
    }

    [Fact]
    public void FindScript_NothingRecordedUnderTheName_ReturnsNull()
        => DatabaseState.Empty.FindExecution(new ScriptReference(null, new SqlIdentifier("seed"))).ShouldBeNull();

    [Fact]
    public void FindScript_SameNameInAnotherScope_ReturnsNull()
    {
        // Arrange — identity is (scope, name): a scoped execution is not found by the global address, nor by
        // another schema's.
        var scoped = new ScriptExecution(new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed")), "abc", _now);
        var state = new DatabaseState(new Database(), [scoped]);

        // Assert
        state.FindExecution(new ScriptReference(null, new SqlIdentifier("seed"))).ShouldBeNull();
        state.FindExecution(new ScriptReference(new SqlIdentifier("billing"), new SqlIdentifier("seed"))).ShouldBeNull();
        state.FindExecution(new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed"))).ShouldBe(scoped);
    }

    [Fact]
    public void RecordScripts_SameNameInAnotherScope_DoesNotReplace()
    {
        // Arrange
        var global = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("seed")), "abc", _now);
        var state = new DatabaseState(new Database(), [global]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution(new ScriptReference(new SqlIdentifier("sales"), new SqlIdentifier("seed")), "def", _now)]);

        // Assert
        recorded.Scripts.Count.ShouldBe(2);
    }

    [Fact]
    public void RemoveScript_RemovesTheEntryByName_CaseInsensitively()
    {
        // Arrange
        var other = new ScriptExecution(new ScriptReference(null, new SqlIdentifier("api-login")), "hash", _now);
        var state = new DatabaseState(new Database(), [new ScriptExecution(new ScriptReference(null, new SqlIdentifier("Seed")), "abc", _now), other]);

        // Act
        var removed = state.RemoveExecution(new ScriptReference(null, new SqlIdentifier("seed")));

        // Assert
        removed.Scripts.ShouldBe([other]);
    }

    [Fact]
    public void RemoveScript_NothingRecordedUnderTheName_ReturnsTheSameState()
        => DatabaseState.Empty.RemoveExecution(new ScriptReference(null, new SqlIdentifier("seed"))).ShouldBeSameAs(DatabaseState.Empty);
}
