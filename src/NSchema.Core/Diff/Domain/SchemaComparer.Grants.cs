using NSchema.Diff.Domain.Models.Tables;
using NSchema.Diff.Domain.Models;
using NSchema.Project.Domain.Models.Schemas;
using NSchema.Project.Domain.Models.Tables;

namespace NSchema.Diff.Domain;

internal sealed partial class SchemaComparer
{
    private List<GrantChange> CompareSchemaGrants(string schemaName, IReadOnlyList<SchemaGrant> current, IReadOnlyList<SchemaGrant> desired)
    {
        var result = new List<GrantChange>();
        foreach (var g in current.Where(c => desired.All(d => d.Role != c.Role)))
        {
            LogSchemaUsageRevoking(schemaName, g.Role);
            result.Add(new GrantChange(ChangeKind.Remove, g.Role, null));
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogSchemaUsageGranting(schemaName, g.Role);
            result.Add(new GrantChange(ChangeKind.Add, g.Role, null));
        }
        return result;
    }

    private List<GrantChange> CompareTableGrants(string schemaName, string tableName, IReadOnlyList<TableGrant> current, IReadOnlyList<TableGrant> desired)
    {
        var result = new List<GrantChange>();
        foreach (var g in current)
        {
            var matching = desired.FirstOrDefault(d => d.Role == g.Role);
            if (matching is null)
            {
                LogTablePrivilegesRevoking(schemaName, tableName, g.Role);
                result.Add(new GrantChange(ChangeKind.Remove, g.Role, g.Privileges));
            }
            else if (matching.Privileges != g.Privileges)
            {
                LogTablePrivilegesUpdating(schemaName, tableName, g.Role);
                result.Add(new GrantChange(ChangeKind.Remove, g.Role, g.Privileges));
                result.Add(new GrantChange(ChangeKind.Add, g.Role, matching.Privileges));
            }
        }
        foreach (var g in desired.Where(d => current.All(c => c.Role != d.Role)))
        {
            LogTablePrivilegesGranting(schemaName, tableName, g.Role);
            result.Add(new GrantChange(ChangeKind.Add, g.Role, g.Privileges));
        }
        return result;
    }
}
