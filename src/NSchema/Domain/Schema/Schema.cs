namespace NSchema.Domain.Schema;

public record Schema(
    string Name,
    IReadOnlyList<Table> Tables,
    string? PreviousName = null
);
