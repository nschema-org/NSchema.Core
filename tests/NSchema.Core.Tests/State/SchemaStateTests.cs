using NSchema.Schema.Model;
using NSchema.Schema.Model.Scripts;
using NSchema.State.Model;

namespace NSchema.Tests.State;

public sealed class SchemaStateTests
{
    private static readonly DateTimeOffset _now = new(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RecordScripts_AddsEntriesToTheLedger()
    {
        // Arrange
        var state = SchemaState.Empty;

        // Act
        var recorded = state.RecordExecution([new ScriptExecution("seed", "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().ShouldBe(new ScriptExecution("seed", "abc", _now));
    }

    [Fact]
    public void RecordScripts_ReplacesAnEarlierExecutionByName_CaseInsensitively()
    {
        // Arrange
        var state = new SchemaState(new DatabaseSchema(), [new ScriptExecution("Seed", "old", DateTimeOffset.UnixEpoch)]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution("seed", "new", _now)]);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().Hash.ShouldBe("new");
    }

    [Fact]
    public void RecordScripts_LeavesOtherEntriesAlone()
    {
        // Arrange
        var existing = new ScriptExecution("api-login", "hash", DateTimeOffset.UnixEpoch);
        var state = new SchemaState(new DatabaseSchema(), [existing]);

        // Act
        var recorded = state.RecordExecution([new ScriptExecution("seed", "abc", _now)]);

        // Assert
        recorded.Scripts.ShouldBe([existing, new ScriptExecution("seed", "abc", _now)]);
    }

    [Fact]
    public void RecordScripts_NothingExecuted_ReturnsTheSameState()
        => SchemaState.Empty.RecordExecution([]).ShouldBeSameAs(SchemaState.Empty);

    [Fact]
    public void FindScript_MatchesByName_CaseInsensitively()
    {
        // Arrange
        var existing = new ScriptExecution("Seed", "abc", _now);
        var state = new SchemaState(new DatabaseSchema(), [existing]);

        // Act
        var found = state.FindExecution("seed");

        // Assert
        found.ShouldBe(existing);
    }

    [Fact]
    public void FindScript_NothingRecordedUnderTheName_ReturnsNull()
        => SchemaState.Empty.FindExecution("seed").ShouldBeNull();

    [Fact]
    public void RemoveScript_RemovesTheEntryByName_CaseInsensitively()
    {
        // Arrange
        var other = new ScriptExecution("api-login", "hash", _now);
        var state = new SchemaState(new DatabaseSchema(), [new ScriptExecution("Seed", "abc", _now), other]);

        // Act
        var removed = state.RemoveExecution("seed");

        // Assert
        removed.Scripts.ShouldBe([other]);
    }

    [Fact]
    public void RemoveScript_NothingRecordedUnderTheName_ReturnsTheSameState()
        => SchemaState.Empty.RemoveExecution("seed").ShouldBeSameAs(SchemaState.Empty);
}
