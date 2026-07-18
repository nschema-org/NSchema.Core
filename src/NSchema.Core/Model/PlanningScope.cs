using System.Diagnostics.CodeAnalysis;

namespace NSchema.Model;

/// <summary>
/// What part of the database a run is about.
/// </summary>
/// <remarks>
/// An entry covers itself and everything below it, but never drags its container's concerns into the run.
/// </remarks>
public sealed record PlanningScope
{
    private PlanningScope(IReadOnlyList<SqlIdentifier> schemas, IReadOnlyList<ObjectAddress> objects, bool unscoped)
    {
        Schemas = schemas;
        Objects = objects;
        SchemaNames = unscoped ? null : [.. schemas.Concat(objects.Select(o => o.Schema)).Distinct()];
    }

    /// <summary>
    /// Every schema the source can see.
    /// </summary>
    public static PlanningScope All { get; } = new([], [], unscoped: true);

    /// <summary>
    /// A scope covering the named schemas wholly (case-insensitive). An empty set normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="schemaNames">The schema names under management.</param>
    public static PlanningScope To(params IEnumerable<SqlIdentifier> schemaNames) => To(schemaNames, []);

    /// <summary>
    /// A scope covering the addressed objects alone. An empty set normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="objects">The addresses of the objects under management.</param>
    public static PlanningScope To(IEnumerable<ObjectAddress> objects) => To([], objects);

    /// <summary>
    /// A scope covering the named schemas wholly and the addressed objects alone.
    /// Empty of both normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="schemaNames">The schema names under management.</param>
    /// <param name="objects">The addresses of the objects under management.</param>
    public static PlanningScope To(IEnumerable<SqlIdentifier> schemaNames, IEnumerable<ObjectAddress> objects)
    {
        var names = schemaNames.Distinct().ToList();
        var addresses = objects.Where(o => !names.Contains(o.Schema)).Distinct().ToList();
        return names.Count == 0 && addresses.Count == 0 ? All : new PlanningScope(names, addresses, unscoped: false);
    }

    /// <summary>
    /// The names of the wholly-covered schemas; empty when the scope is <see cref="All"/> or objects-only.
    /// </summary>
    public IReadOnlyList<SqlIdentifier> Schemas { get; }

    /// <summary>
    /// The addresses of the objects the scope covers individually, beyond the wholly-covered schemas.
    /// </summary>
    public IReadOnlyList<ObjectAddress> Objects { get; }

    /// <summary>
    /// Every schema this scope reads — the named schemas plus each addressed object's schema — or
    /// <see langword="null"/> when the scope is <see cref="All"/>.
    /// </summary>
    public IReadOnlyList<SqlIdentifier>? SchemaNames { get; }

    /// <summary>
    /// Whether this scope includes every schema.
    /// </summary>
    [MemberNotNullWhen(false, nameof(SchemaNames))]
    public bool IsUnscoped => SchemaNames is null;

    /// <summary>
    /// Whether the scope covers the named schema and everything inside it.
    /// </summary>
    public bool Contains(SqlIdentifier schemaName) => IsUnscoped || Schemas.Contains(schemaName);

    /// <summary>
    /// Whether the scope covers the addressed object: its schema is wholly covered, or it is addressed itself.
    /// </summary>
    public bool Contains(ObjectAddress address) => Contains(address.Schema) || Objects.Contains(address);

    /// <summary>
    /// Whether the scope covers the identified object.
    /// </summary>
    public bool Contains(ObjectIdentity identity) => Contains(identity.Address);
}
