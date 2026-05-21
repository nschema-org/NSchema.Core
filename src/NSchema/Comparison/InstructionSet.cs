using NSchema.Domain.Migration.Instructions;

namespace NSchema.Comparison;

internal sealed class InstructionSet
{
    private readonly List<SchemaInstruction> _preScripts = [];
    private readonly List<SchemaInstruction> _foreignKeyDrops = [];
    private readonly List<SchemaInstruction> _indexDrops = [];
    private readonly List<SchemaInstruction> _primaryKeyDrops = [];
    private readonly List<SchemaInstruction> _schemaRenames = [];
    private readonly List<SchemaInstruction> _schemaCreates = [];
    private readonly List<SchemaInstruction> _tableRenames = [];
    private readonly List<SchemaInstruction> _tableCreates = [];
    private readonly List<SchemaInstruction> _columnDrops = [];
    private readonly List<SchemaInstruction> _columnRenames = [];
    private readonly List<SchemaInstruction> _columnAdds = [];
    private readonly List<SchemaInstruction> _columnAlters = [];
    private readonly List<SchemaInstruction> _primaryKeyAdds = [];
    private readonly List<SchemaInstruction> _foreignKeyAdds = [];
    private readonly List<SchemaInstruction> _indexAdds = [];
    private readonly List<SchemaInstruction> _tableDrops = [];
    private readonly List<SchemaInstruction> _schemaDrops = [];
    private readonly List<SchemaInstruction> _postScripts = [];

    public void Add(SchemaInstruction instruction)
    {
        var bucket = instruction switch
        {
            RunPreDeploymentScript => _preScripts,
            RunPostDeploymentScript => _postScripts,
            DropForeignKey => _foreignKeyDrops,
            DropIndex => _indexDrops,
            DropPrimaryKey => _primaryKeyDrops,
            RenameSchema => _schemaRenames,
            CreateSchema => _schemaCreates,
            RenameTable => _tableRenames,
            CreateTable => _tableCreates,
            DropColumn => _columnDrops,
            RenameColumn => _columnRenames,
            AddColumn => _columnAdds,
            AlterColumnType => _columnAlters,
            AlterColumnNullability => _columnAlters,
            SetColumnDefault => _columnAlters,
            AddPrimaryKey => _primaryKeyAdds,
            AddForeignKey => _foreignKeyAdds,
            CreateIndex => _indexAdds,
            DropTable => _tableDrops,
            DropSchema => _schemaDrops,
            _ => throw new InvalidOperationException($"Unhandled instruction type: {instruction.GetType().Name}")
        };
        bucket.Add(instruction);
    }

    public List<SchemaInstruction> ToList() =>
    [
        .._preScripts,
        .._foreignKeyDrops,
        .._indexDrops,
        .._primaryKeyDrops,
        .._schemaRenames,
        .._schemaCreates,
        .._tableRenames,
        .._tableCreates,
        .._columnDrops,
        .._columnRenames,
        .._columnAdds,
        .._columnAlters,
        .._primaryKeyAdds,
        .._foreignKeyAdds,
        .._indexAdds,
        .._tableDrops,
        .._schemaDrops,
        .._postScripts,
    ];
}
