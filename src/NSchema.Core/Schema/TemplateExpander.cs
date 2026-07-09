using NSchema.Schema.Model;
using NSchema.Schema.Model.Columns;
using NSchema.Schema.Model.Migrations;
using NSchema.Schema.Model.Schemas;
using NSchema.Schema.Model.Tables;
using NSchema.Schema.Model.Templates;
using NSchema.Schema.Model.Triggers;

namespace NSchema.Schema;

/// <summary>
/// Expands template applications into concrete schema objects.
/// </summary>
internal static class TemplateExpander
{
    /// <summary>
    /// Expands templates into a given schema, returning the expanded schema alongside the data migrations the
    /// applications instantiated (one per template migration per applied schema).
    /// </summary>
    public static (DatabaseSchema Schema, IReadOnlyList<DataMigration> Migrations) Expand(DatabaseSchema schema, TemplateSet templates)
    {
        var byName = new Dictionary<string, TemplateDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates.Definitions)
        {
            if (!byName.TryAdd(template.Name, template))
            {
                throw new InvalidOperationException($"Duplicate template '{template.Name}' declared.");
            }
        }

        var pendingIncludes = templates.Includes.ToList();
        var migrations = new List<DataMigration>();
        foreach (var application in templates.Applications)
        {
            if (!byName.TryGetValue(application.TemplateName, out var template))
            {
                throw new InvalidOperationException($"APPLY TEMPLATE references unknown template '{application.TemplateName}'.");
            }
            if (template.Kind != TemplateKind.Schema)
            {
                throw new InvalidOperationException(
                    $"APPLY TEMPLATE targets schemas, but '{template.Name}' is a table template; include it from a table body with INCLUDE.");
            }

            foreach (var schemaName in application.SchemaNames)
            {
                if (!schema.Schemas.Any(s => string.Equals(s.Name, schemaName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"APPLY TEMPLATE '{template.Name}' targets unknown schema '{schemaName}'; declare it with CREATE SCHEMA.");
                }

                // Combine performs the merge and rejects an object the target schema already declares, exactly as
                // if the instantiated objects had been written in the target schema by hand.
                schema = schema.Combine(new DatabaseSchema([Apply(template, schemaName)]));

                // The template's own includes re-target from the placeholder to this instance's schema and
                // resolve with everything else below, so an instantiated table can itself include a template.
                pendingIncludes.AddRange(template.Includes.Select(i => i with { SchemaName = schemaName }));

                migrations.AddRange(template.Migrations.Select(m => Instantiate(m, schemaName)));
            }
        }

        return (ResolveIncludes(schema, byName, pendingIncludes), migrations);
    }

    /// <summary>
    /// Re-homes a template migration into <paramref name="schemaName"/>. The raw SQL body cannot be re-bound the
    /// way parsed statements are, so the <c>{schema}</c> token is its stand-in for the target schema.
    /// </summary>
    private static DataMigration Instantiate(DataMigration migration, string schemaName) => migration with
    {
        SchemaName = schemaName,
        Sql = migration.Sql.Replace("{schema}", schemaName, StringComparison.Ordinal),
    };

    private static DatabaseSchema ResolveIncludes(
        DatabaseSchema schema,
        Dictionary<string, TemplateDefinition> templates,
        List<TemplateInclude> includes)
    {
        if (includes.Count == 0)
        {
            return schema;
        }

        var byTable = includes
            .GroupBy(i => Key(i.SchemaName, i.TableName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var consumed = new HashSet<string>();
        var resolved = schema.Schemas
            .Select(definition => definition with
            {
                Tables = definition.Tables
                    .Select(table =>
                    {
                        var key = Key(definition.Name, table.Name);
                        if (!byTable.TryGetValue(key, out var tableIncludes))
                        {
                            return table;
                        }
                        consumed.Add(key);
                        return MergeIncludes(definition.Name, table, tableIncludes, templates);
                    })
                    .ToList(),
            })
            .ToList();

        // Parsed includes always name the table whose body they were written in, so a dangling one can only come
        // from a hand-built document; fail rather than drop it silently.
        var dangling = byTable.Keys.FirstOrDefault(key => !consumed.Contains(key));
        if (dangling is not null)
        {
            var include = byTable[dangling][0];
            throw new InvalidOperationException(
                $"INCLUDE '{include.TemplateName}' targets unknown table '{include.SchemaName}.{include.TableName}'.");
        }

        return schema with { Schemas = resolved };
    }

    // The NUL character cannot appear in an identifier, so it is a safe composite-key separator.
    private static string Key(string schema, string table) => $"{schema.ToLowerInvariant()}\0{table.ToLowerInvariant()}";

    /// <summary>
    /// Merges each included table template's members into <paramref name="table"/>: columns land at the position
    /// the include was written, foreign keys referencing the placeholder re-point at the including table's schema,
    /// and everything else appends. A member the table already declares is rejected.
    /// </summary>
    private static Table MergeIncludes(string schemaName, Table table, List<TemplateInclude> includes, Dictionary<string, TemplateDefinition> templates)
    {
        var qualified = $"{schemaName}.{table.Name}";
        var columns = table.Columns.ToList();
        var foreignKeys = table.ForeignKeys.ToList();
        var uniqueConstraints = table.UniqueConstraints.ToList();
        var checkConstraints = table.CheckConstraints.ToList();
        var exclusionConstraints = table.ExclusionConstraints.ToList();
        var indexes = table.Indexes.ToList();
        var primaryKey = table.PrimaryKey;

        var offset = 0;
        foreach (var include in includes)
        {
            if (!templates.TryGetValue(include.TemplateName, out var template))
            {
                throw new InvalidOperationException($"Table '{qualified}' includes unknown template '{include.TemplateName}'.");
            }
            if (template.Kind != TemplateKind.Table)
            {
                throw new InvalidOperationException(
                    $"Table '{qualified}' includes '{template.Name}', which is a schema template; only a FOR TABLE template can be included.");
            }

            var members = template.Objects.Tables.Single();

            foreach (var column in members.Columns)
            {
                if (columns.Any(c => string.Equals(c.Name, column.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException(
                        $"Template '{template.Name}' adds column '{column.Name}' to '{qualified}', which already declares it.");
                }
            }
            columns.InsertRange(include.ColumnPosition + offset, members.Columns);
            offset += members.Columns.Count;

            if (members.PrimaryKey is not null)
            {
                if (primaryKey is not null)
                {
                    throw new InvalidOperationException(
                        $"Template '{template.Name}' adds a primary key to '{qualified}', which already declares one.");
                }
                primaryKey = members.PrimaryKey;
            }

            foreignKeys.AddRange(members.ForeignKeys.Select(fk =>
                fk.ReferencedSchema == TemplateDefinition.TargetSchemaPlaceholder
                    ? fk with { ReferencedSchema = schemaName }
                    : fk));
            uniqueConstraints.AddRange(members.UniqueConstraints);
            checkConstraints.AddRange(members.CheckConstraints);
            exclusionConstraints.AddRange(members.ExclusionConstraints);
            indexes.AddRange(members.Indexes);
        }

        return table with
        {
            Columns = columns,
            PrimaryKey = primaryKey,
            ForeignKeys = foreignKeys,
            UniqueConstraints = uniqueConstraints,
            CheckConstraints = checkConstraints,
            ExclusionConstraints = exclusionConstraints,
            Indexes = indexes,
        };
    }

    /// <summary>
    /// Re-homes the template's objects into <paramref name="schemaName"/>: references to the placeholder schema
    /// (unqualified names in the body) re-point at the target, and an unqualified user-defined column type or
    /// trigger function qualifies to the target when the template itself declares it. An unqualified name the
    /// template does not declare is left alone — a built-in type, or a name the database resolves by search path.
    /// </summary>
    private static SchemaDefinition Apply(TemplateDefinition template, string schemaName)
    {
        var objects = template.Objects;

        var declaredTypes = objects.Enums.Select(e => e.Name)
            .Concat(objects.Domains.Select(d => d.Name))
            .Concat(objects.CompositeTypes.Select(t => t.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var declaredRoutines = objects.Routines.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return objects with
        {
            Name = schemaName,
            Tables = objects.Tables.Select(table => table with
            {
                Columns = table.Columns.Select(c => c with { Type = Qualify(c.Type, declaredTypes, schemaName) }).ToList(),
                ForeignKeys = table.ForeignKeys
                    .Select(fk => fk.ReferencedSchema == TemplateDefinition.TargetSchemaPlaceholder
                        ? fk with { ReferencedSchema = schemaName }
                        : fk)
                    .ToList(),
                Triggers = table.Triggers.Select(t => Qualify(t, declaredRoutines, schemaName)).ToList(),
            }).ToList(),
            Domains = objects.Domains.Select(d => d with { DataType = Qualify(d.DataType, declaredTypes, schemaName) }).ToList(),
            CompositeTypes = objects.CompositeTypes
                .Select(t => t with { Fields = t.Fields.Select(f => f with { DataType = Qualify(f.DataType, declaredTypes, schemaName) }).ToList() })
                .ToList(),
        };
    }

    private static SqlType Qualify(SqlType type, HashSet<string> declaredTypes, string schemaName)
    {
        if (type.Name.Contains('.'))
        {
            return type; // explicitly qualified — escapes the template
        }

        // A custom type may carry its facets in the name text; only the base name identifies it.
        var paren = type.Name.IndexOf('(');
        var baseName = paren < 0 ? type.Name : type.Name[..paren];
        if (!declaredTypes.Contains(baseName))
        {
            return type;
        }

        // A fresh SqlType lower-cases the qualified name through its primary constructor (a with-expression would
        // bypass that normalization); the facet properties carry over.
        return new SqlType($"{schemaName}.{type.Name}") { Length = type.Length, Precision = type.Precision, Scale = type.Scale };
    }

    private static Trigger Qualify(Trigger trigger, HashSet<string> declaredRoutines, string schemaName)
        => trigger.Function is { } function && !function.Contains('.') && declaredRoutines.Contains(function)
            ? trigger with { Function = $"{schemaName}.{function}" }
            : trigger;
}
