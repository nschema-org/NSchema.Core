using NSchema.Policies;
using NSchema.Schema.Model;

namespace NSchema.Schema;

internal class SchemaPolicyValidator(IEnumerable<ISchemaPolicy> schemaPolicies)
{
    public PolicyDiagnostics Validate(DatabaseSchema schema)
    {
        var schemaDiagnostics = schemaPolicies.SelectMany(p => p.Validate(schema));
        return new PolicyDiagnostics(schemaDiagnostics);
    }
}
