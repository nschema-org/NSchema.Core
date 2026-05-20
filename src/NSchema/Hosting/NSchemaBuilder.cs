using NSchema.Domain.Schema;
using NSchema.Execution;
using NSchema.Extractors;
using NSchema.Fluent;

namespace NSchema.Hosting;

public sealed class NSchemaBuilder
{
    internal ISchemaExtractor? Extractor { get; private set; }
    internal IInstructionExecutor? Executor { get; private set; }
    internal DatabaseModel? Model { get; private set; }
    internal ExecutionOptions ExecutionOptions { get; private set; } = new();

    public NSchemaBuilder UseExtractor(ISchemaExtractor extractor) { Extractor = extractor; return this; }
    public NSchemaBuilder UseExecutor(IInstructionExecutor executor) { Executor = executor; return this; }

    public NSchemaBuilder WithModel(DatabaseModel model) { Model = model; return this; }

    public NSchemaBuilder WithModel(Action<DatabaseModelBuilder> configure)
    {
        var builder = new DatabaseModelBuilder();
        configure(builder);
        Model = builder.Build();
        return this;
    }

    public NSchemaBuilder OnDestructiveAction(DestructiveActionPolicy policy)
    {
        ExecutionOptions = new ExecutionOptions(policy);
        return this;
    }
}
