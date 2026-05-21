using NSchema.Migration;

namespace NSchema;

public class MigrationOptions
{
    public DestructiveActionPolicy DestructiveActionPolicy { get; set; } = DestructiveActionPolicy.Error;
}
