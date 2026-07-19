using NSchema.Model;
using NSchema.Project.Model.Directives;

namespace NSchema.Diff.Model.Services;

/// <summary>
/// Rewrites the current schema into the declared name-space by applying the project's rename directives.
/// </summary>
internal static class DatabaseAligner
{
    public static Result<AlignedDatabase> Align(Database current, Database desired, ProjectDirectives directives)
    {
        var diagnostics = new List<Diagnostic>();

        // Applied renames, keyed by current names (what the tree carries before alignment); the logs are the
        // same renames keyed by declared names (what the tree carries after).
        var schemaRenames = new Dictionary<SqlIdentifier, SqlIdentifier>();
        var schemaLog = new Dictionary<SqlIdentifier, SqlIdentifier>();
        var objectRenames = new Dictionary<ObjectIdentity, SqlIdentifier>();
        var objectLog = new Dictionary<ObjectIdentity, SqlIdentifier>();
        var columnRenames = new Dictionary<MemberAddress, SqlIdentifier>();
        var columnLog = new Dictionary<MemberAddress, SqlIdentifier>();

        // Schema renames resolve first: object and column directives address current reality, but their
        // entities land in the declared schema, so the logs key through the applied schema renames.
        foreach (var rename in directives.SchemaRenames)
        {
            if (current.Schemas.All(s => s.Name != rename.From))
            {
                if (current.Schemas.Any(s => s.Name == rename.To))
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename("schema", rename.From.Value, rename.To));
                }
                continue;
            }

            if (desired.Schemas.Any(s => s.Name == rename.From))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared("schema", rename.To.Value, rename.From));
                continue;
            }
            if (current.Schemas.Any(s => s.Name == rename.To))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken("schema", rename.To.Value, rename.From, rename.To));
                continue;
            }

            schemaRenames[rename.From] = rename.To;
            schemaLog[rename.To] = rename.From;
        }

        foreach (var rename in directives.ObjectRenames)
        {
            var kind = rename.From.Kind;
            var schema = current.Schemas.FirstOrDefault(s => s.Name == rename.From.Schema);
            if (schema is null || schema.Objects().All(o => o.Kind != kind || o.Name != rename.From.Name))
            {
                if (schema?.Objects().Any(o => o.Kind == kind && o.Name == rename.To) == true)
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename(kind.Display(), rename.From.ToString(), rename.To));
                }
                continue;
            }

            var declaredSchema = schemaRenames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
            var address = new ObjectAddress(declaredSchema, rename.To).ToString();
            if (desired.Schemas.FirstOrDefault(s => s.Name == declaredSchema)?.Objects()
                    .Any(o => o.Kind == kind && o.Name == rename.From.Name) == true)
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared(kind.Display(), address, rename.From.Name));
                continue;
            }
            if (schema.Objects().Any(o => o.Kind == kind && o.Name == rename.To))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken(kind.Display(), address, rename.From.Name, rename.To));
                continue;
            }

            objectRenames[rename.From] = rename.To;
            objectLog[new ObjectIdentity(kind, declaredSchema, rename.To)] = rename.From.Name;
        }

        foreach (var rename in directives.MemberRenames)
        {
            var table = current.Schemas.FirstOrDefault(s => s.Name == rename.From.Schema)
                ?.Tables.FirstOrDefault(t => t.Name == rename.From.Object);
            if (table is null || table.Columns.All(c => c.Name != rename.From.Member))
            {
                if (table?.Columns.Any(c => c.Name == rename.To) == true)
                {
                    diagnostics.Add(DiffDiagnostics.AppliedRename("column", rename.From.ToString(), rename.To));
                }
                continue;
            }

            var declaredSchema = schemaRenames.GetValueOrDefault(rename.From.Schema, rename.From.Schema);
            var declaredTable = objectRenames.GetValueOrDefault(new ObjectIdentity(ObjectKind.Table, rename.From.Schema, rename.From.Object), rename.From.Object);
            var address = new MemberAddress(declaredSchema, declaredTable, rename.To);
            if (desired.Schemas.FirstOrDefault(s => s.Name == declaredSchema)
                    ?.Tables.FirstOrDefault(t => t.Name == declaredTable)
                    ?.Columns.Any(c => c.Name == rename.From.Member) == true)
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameSourceStillDeclared("column", address.ToString(), rename.From.Member));
                continue;
            }
            if (table.Columns.Any(c => c.Name == rename.To))
            {
                diagnostics.Add(DiffDiagnostics.AmbiguousRenameTargetTaken("column", address.ToString(), rename.From.Member, rename.To));
                continue;
            }

            columnRenames[rename.From] = rename.To;
            columnLog[address] = rename.From.Member;
        }

        if (schemaRenames.Count == 0 && objectRenames.Count == 0 && columnRenames.Count == 0)
        {
            return Result.From(AlignedDatabase.Unaligned(current), diagnostics);
        }

        // Apply the renames to a copy — the observation is shared with the rest of the run (widening, drift
        // display), so it is not ours to mutate. The rename maps key by current names, so renames apply
        // inside-out: columns while their table still carries its current name, then objects, then the schema.
        var aligned = current.Clone();
        foreach (var schema in aligned.Schemas)
        {
            var currentSchemaName = schema.Name;
            foreach (var table in schema.Tables)
            {
                foreach (var column in table.Columns)
                {
                    if (columnRenames.TryGetValue(new MemberAddress(currentSchemaName, table.Name, column.Name), out var columnName))
                    {
                        column.Name = columnName;
                    }
                }
            }
            foreach (var obj in schema.Objects())
            {
                if (objectRenames.TryGetValue(new ObjectIdentity(obj.Kind, currentSchemaName, obj.Name), out var objectName))
                {
                    obj.Name = objectName;
                }
            }
            if (schemaRenames.TryGetValue(currentSchemaName, out var schemaName))
            {
                schema.Name = schemaName;
            }
        }

        return Result.From(new AlignedDatabase(aligned, new RenameLog(schemaLog, objectLog, columnLog)), diagnostics);
    }
}
