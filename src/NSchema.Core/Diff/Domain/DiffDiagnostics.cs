using NSchema.Model;
using NSchema.Model.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// The diagnostics minted while computing the diff.
/// </summary>
internal static class DiffDiagnostics
{
    /// <summary>
    /// Objects outside the scope that this run's removals cost, and so must go with them.
    /// </summary>
    public static Diagnostic SeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan reaches outside its scope: {Render(addresses)} depend on objects it removes, so they are " +
        "removed too. Narrow what you are dropping, or drop these deliberately.");

    /// <summary>
    /// The same, for objects reached only through an edge NSchema inferred rather than one the model states.
    /// </summary>
    public static Diagnostic InferredSeveredOutOfScope(IEnumerable<Address> addresses) => Diagnostic.Warning("scope",
        $"This plan also removes {Render(addresses)}, which appear to read something it removes. NSchema does " +
        "not parse SQL, so it reads view bodies to find this — check these before applying: a body it misread " +
        "means removing something that did not need to go.");

    private static string Render(IEnumerable<Address> addresses) =>
        string.Join(", ", addresses.Select(a => $"'{a}'"));

    /// <summary>
    /// A run-once script whose body has changed since its recorded execution; it stays skipped.
    /// </summary>
    public static Diagnostic ChangedRunOnceScript(DeploymentScript script) => Diagnostic.Warning("run-once",
        $"Run-once script '{script.Reference}' has changed since it was executed and stays skipped.");

    /// <summary>
    /// A change-event script that matches nothing in this plan and will not run.
    /// </summary>
    public static Diagnostic DeadMigration(ChangeScript migration) => Diagnostic.Info("data-migrations",
        $"Migration '{migration.Reference}' ({migration.Description}) matches " +
        "no change in this plan and will not run. If the change it supports has been applied everywhere, the block is safe to delete.");

    /// <summary>
    /// A rename directive whose source is gone and whose target already exists.
    /// </summary>
    public static Diagnostic AppliedRename(string kind, string address, SqlIdentifier to) => Diagnostic.Info("directives",
        $"Rename of {kind} '{address}' matches nothing in the current state and '{to}' already exists — the " +
        "rename has been applied. Once it has been applied everywhere, the directive is safe to delete.");
}
