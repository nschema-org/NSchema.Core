using NSchema.Deployment;
using NSchema.Deployment.Backends;
using NSchema.Diff.Domain.Models;
using NSchema.Model;
using NSchema.Operations;
using NSchema.Plan.Backends;
using NSchema.Plan.PlanFile;
using NSchema.Plan.Policies;
using NSchema.Plugins;
using NSchema.Project;
using NSchema.Project.Policies;
using NSchema.State;
using NSchema.State.Backends;
using NSchema.State.Locks;
using NSchema.State.Locks.Backends;

namespace NSchema.Tests.Architecture;

/// <summary>
/// The explicit classification registry for every public interface in Core. A public interface is
/// contract; each one must declare which kind of contract it is. Adding a public interface without
/// classifying it here fails the build — decide what it is before shipping it.
/// </summary>
public sealed class PublicInterfaceClassificationTests
{
    /// <summary>
    /// The kinds of public contract Core exposes.
    /// </summary>
    private enum InterfaceRole
    {
        /// <summary>
        /// Consumer surface, reached through <see cref="NSchemaApplication"/>.
        /// </summary>
        ApplicationSeam,

        /// <summary>
        /// SPI implemented downstream by providers and backends.
        /// </summary>
        BackendSpi,

        /// <summary>
        /// DomainType extension point validated during planning.
        /// </summary>
        PolicySeam,

        /// <summary>
        /// Shape contract implemented by domain model types.
        /// </summary>
        ModelContract,

        /// <summary>
        /// Crosses an application seam and a backend SPI; lives at its cluster root.
        /// </summary>
        SharedContract,
    }

    private static readonly IReadOnlyDictionary<Type, InterfaceRole> _registry = new Dictionary<Type, InterfaceRole>
    {
        // Application seams — the consumer surface on NSchemaApplication.
        [typeof(INSchemaOperations)] = InterfaceRole.ApplicationSeam,
        [typeof(IDatabaseProvider)] = InterfaceRole.ApplicationSeam,
        [typeof(IProjectProvider)] = InterfaceRole.ApplicationSeam,
        [typeof(IPlanFileManager)] = InterfaceRole.ApplicationSeam,
        [typeof(IStateLockManager)] = InterfaceRole.ApplicationSeam,
        [typeof(IDatabaseStateManager)] = InterfaceRole.ApplicationSeam,

        // Backend SPIs — implemented by provider/backend packages.
        [typeof(IDatabaseIntrospector)] = InterfaceRole.BackendSpi,
        [typeof(ISqlDialect)] = InterfaceRole.BackendSpi,
        [typeof(IDatabaseStateStore)] = InterfaceRole.BackendSpi,
        [typeof(IStateLock)] = InterfaceRole.BackendSpi,
        [typeof(INSchemaPlugin)] = InterfaceRole.BackendSpi,
        [typeof(INSchemaProviderPlugin)] = InterfaceRole.BackendSpi,
        [typeof(INSchemaBackendPlugin)] = InterfaceRole.BackendSpi,

        // Policy seams — domain extension points.
        [typeof(IProjectPolicy)] = InterfaceRole.PolicySeam,
        [typeof(IPlanPolicy)] = InterfaceRole.PolicySeam,

        // Model contracts — shapes on the domain models.
        [typeof(INamedObject)] = InterfaceRole.ModelContract,
        [typeof(INamedObjectDiff)] = InterfaceRole.ModelContract,
        [typeof(ISchemaObjectDiff)] = InterfaceRole.ModelContract,

        // Shared contracts — returned by the manager seam, produced by the lock SPI.
        [typeof(IStateLockHandle)] = InterfaceRole.SharedContract,
    };

    [Fact]
    public void EveryPublicInterface_IsClassified()
    {
        // Arrange
        var publicInterfaces = ArchitectureTestSupport.CoreAssembly.GetExportedTypes()
            .Where(t => t.IsInterface)
            .ToList();

        // Act
        var unclassified = publicInterfaces.Except(_registry.Keys).Select(t => t.FullName).ToList();

        // Assert
        publicInterfaces.ShouldNotBeEmpty();
        unclassified.ShouldBeEmpty($"Unclassified public interfaces (add them to the registry with a role): {string.Join(", ", unclassified)}");
    }

    [Fact]
    public void Registry_ContainsOnlyCurrentPublicInterfaces()
    {
        // Act — an entry goes stale when the type stops being a public interface in Core.
        var stale = _registry.Keys
            .Where(t => !t.IsInterface || !t.IsPublic || t.Assembly != ArchitectureTestSupport.CoreAssembly)
            .Select(t => t.FullName)
            .ToList();

        // Assert
        stale.ShouldBeEmpty($"Stale registry entries: {string.Join(", ", stale)}");
    }
}
