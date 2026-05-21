namespace NSchema.Domain.Migration.Actions;

public abstract record SchemaAction
{
    public abstract bool IsDestructive { get; }
}
