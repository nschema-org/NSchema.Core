using NSchema.Project.Domain.Models.Scripts;

namespace NSchema.Diff.Domain;

/// <summary>
/// The diagnostics minted while computing the diff.
/// </summary>
internal static class DiffDiagnostics
{
    /// <summary>
    /// A run-once script whose body has changed since its recorded execution; it stays skipped.
    /// </summary>
    public static Diagnostic ChangedRunOnceScript(Script script) => Diagnostic.Warning("run-once",
        $"Run-once script '{script.Name}' has changed since it was executed and stays skipped.");

    /// <summary>
    /// A change-event script that matches nothing in this plan and will not run.
    /// </summary>
    public static Diagnostic DeadMigration(Script migration) => Diagnostic.Info("data-migrations",
        $"Migration '{migration.Name}' ({migration.Event.Description}) matches " +
        "no change in this plan and will not run. If the change it supports has been applied everywhere, the block is safe to delete.");
}
