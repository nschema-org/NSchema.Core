namespace NSchema.Migration;

public record MigrationOptions(
    DestructiveActionPolicy DestructiveActionPolicy = DestructiveActionPolicy.Error
);
