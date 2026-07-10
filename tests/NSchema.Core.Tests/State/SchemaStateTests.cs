using NSchema.Schema.Model;
using NSchema.Sql.Model;
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
        var recorded = state.RecordScripts([new ScriptHash("seed", "abc")], _now);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().ShouldBe(new ScriptRecord("seed", "abc", _now));
    }

    [Fact]
    public void RecordScripts_ReplacesAnEarlierExecutionByName_CaseInsensitively()
    {
        // Arrange
        var state = new SchemaState(new DatabaseSchema(), [new ScriptRecord("Seed", "old", DateTimeOffset.UnixEpoch)]);

        // Act
        var recorded = state.RecordScripts([new ScriptHash("seed", "new")], _now);

        // Assert
        recorded.Scripts.ShouldHaveSingleItem().Hash.ShouldBe("new");
    }

    [Fact]
    public void RecordScripts_LeavesOtherEntriesAlone()
    {
        // Arrange
        var existing = new ScriptRecord("api-login", "hash", DateTimeOffset.UnixEpoch);
        var state = new SchemaState(new DatabaseSchema(), [existing]);

        // Act
        var recorded = state.RecordScripts([new ScriptHash("seed", "abc")], _now);

        // Assert
        recorded.Scripts.ShouldBe([existing, new ScriptRecord("seed", "abc", _now)]);
    }

    [Fact]
    public void RecordScripts_NothingExecuted_ReturnsTheSameState()
        => SchemaState.Empty.RecordScripts([], _now).ShouldBeSameAs(SchemaState.Empty);

    [Fact]
    public void FindScript_MatchesByName_CaseInsensitively()
    {
        // Arrange
        var existing = new ScriptRecord("Seed", "abc", _now);
        var state = new SchemaState(new DatabaseSchema(), [existing]);

        // Act
        var found = state.FindScript("seed");

        // Assert
        found.ShouldBe(existing);
    }

    [Fact]
    public void FindScript_NothingRecordedUnderTheName_ReturnsNull()
        => SchemaState.Empty.FindScript("seed").ShouldBeNull();

    [Fact]
    public void RemoveScript_RemovesTheEntryByName_CaseInsensitively()
    {
        // Arrange
        var other = new ScriptRecord("api-login", "hash", _now);
        var state = new SchemaState(new DatabaseSchema(), [new ScriptRecord("Seed", "abc", _now), other]);

        // Act
        var removed = state.RemoveScript("seed");

        // Assert
        removed.Scripts.ShouldBe([other]);
    }

    [Fact]
    public void RemoveScript_NothingRecordedUnderTheName_ReturnsTheSameState()
        => SchemaState.Empty.RemoveScript("seed").ShouldBeSameAs(SchemaState.Empty);
}
