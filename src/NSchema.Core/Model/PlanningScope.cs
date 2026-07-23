namespace NSchema.Model;

/// <summary>
/// What part of the database a run is about.
/// </summary>
/// <remarks>
/// An entry covers itself and everything below it, but never drags its container's concerns into the run.
/// </remarks>
public sealed record PlanningScope
{
    private PlanningScope(IReadOnlyList<Address> addresses) => Addresses = addresses;

    /// <summary>
    /// Every schema the source can see.
    /// </summary>
    public static PlanningScope All { get; } = new([]);

    /// <summary>
    /// A scope covering the given addresses. An empty set normalizes to <see cref="All"/>.
    /// </summary>
    /// <param name="addresses">The addresses under management.</param>
    public static PlanningScope To(params IEnumerable<Address> addresses)
    {
        var wholeSchemas = addresses.OfType<SchemaAddress>().Select(a => a.Schema).ToHashSet();
        // An address whose schema is already covered wholly is redundant.
        var kept = addresses
            .Where(a => a is SchemaAddress || a.SchemaName is not { } schema || !wholeSchemas.Contains(schema))
            .Distinct()
            .ToList();
        return kept.Count == 0 ? All : new PlanningScope(kept);
    }

    /// <summary>
    /// The addresses the scope covers.
    /// </summary>
    public IReadOnlyList<Address> Addresses { get; }

    /// <summary>
    /// Whether this scope covers everything — it holds no addresses to narrow the run.
    /// </summary>
    public bool IsUnscoped => Addresses.Count == 0;

    /// <summary>
    /// Whether the scope covers the named schema and everything inside it.
    /// </summary>
    public bool Contains(SqlIdentifier schemaName) => IsUnscoped || Addresses.Contains(new SchemaAddress(schemaName));

    /// <summary>
    /// Whether the scope covers the addressed object: its schema is wholly covered, or it is addressed itself.
    /// </summary>
    public bool Contains(ObjectAddress address) => Contains(address.Schema) || Addresses.Contains(address);

    /// <summary>
    /// Whether the scope covers the identified object.
    /// </summary>
    public bool Contains(ObjectIdentity identity) => Contains(identity.Address);
}
