namespace NSchema.Schema;

[Flags]
public enum TablePrivilege
{
    None     = 0,
    Select   = 1 << 0,
    Insert   = 1 << 1,
    Update   = 1 << 2,
    Delete   = 1 << 3,

    ReadOnly   = Select,
    AppendOnly = Select | Insert,
    All        = Select | Insert | Update | Delete,
}
