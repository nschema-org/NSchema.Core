using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using NSchema.Operations;

namespace NSchema.Tests.Architecture;

/// <summary>
/// Shared plumbing for the architecture tests.
/// </summary>
internal static class ArchitectureTestSupport
{
    /// <summary>
    /// The <c>NSchema.Core</c> assembly under test.
    /// </summary>
    public static readonly Assembly CoreAssembly = typeof(INSchemaOperations).Assembly;

    /// <summary>
    /// The loaded architecture of <c>NSchema.Core</c>, built once and shared by every rule.
    /// </summary>
    public static readonly ArchUnitNET.Domain.Architecture Architecture = new ArchLoader().LoadAssemblies(CoreAssembly).Build();

    /// <summary>
    /// Returns a regex matching the given namespaces and everything nested beneath them.
    /// </summary>
    public static string Subtree(params string[] roots) =>
        $"^(?:{string.Join("|", roots.Select(Regex.Escape))})(?:$|\\.)";

    extension(IArchRule rule)
    {
        /// <summary>
        /// Asserts an ArchUnitNET rule passed, naming the violations on failure.
        /// </summary>
        public void ShouldBeSatisfied()
        {
            var violations = rule.Evaluate(Architecture)
                .Where(r => !r.Passed)
                .Select(r => r.Description)
                .ToList();
            violations.ShouldBeEmpty($"Violations: {string.Join(Environment.NewLine, violations)}");
        }
    }
}
