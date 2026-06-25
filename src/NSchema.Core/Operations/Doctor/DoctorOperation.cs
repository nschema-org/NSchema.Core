using Microsoft.Extensions.DependencyInjection;
using NSchema.Schema;
using NSchema.State;
using NSchema.State.Model;

namespace NSchema.Operations.Doctor;

/// <summary>
/// Probes the configured infrastructure end to end.
/// </summary>
/// <remarks>
/// The building blocks are injected directly (rather than via <c>ICurrentSchemaProvider</c>) so a missing source reads as "not configured" and the like.
/// </remarks>
internal sealed class DoctorOperation(
    IOperationReporter reporter,
    ISchemaStateSerializer serializer,
    IStateLock stateLock,
    [FromKeyedServices(NSchemaKeys.OnlineSchemaProvider)]
    ISchemaProvider? online = null,
    ISchemaStateStore? store = null
) : IDoctorOperation
{
    public async Task Execute(DoctorArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Running diagnostics...");

        var failures = 0;
        failures += await CheckDatabase(cancellationToken);
        failures += await CheckStateStore(cancellationToken);

        // The lock check only means anything when a state backend is configured; without one the registered lock is the
        // no-op default, whose acquisition proves nothing.
        if (store is not null)
        {
            failures += await CheckStateLock(cancellationToken);
        }

        if (failures > 0)
        {
            throw new InvalidOperationException($"Diagnostics found {failures} problem{(failures == 1 ? "" : "s")}. See the messages above.");
        }

        reporter.Success("All checks passed.");
    }

    private async Task<int> CheckDatabase(CancellationToken cancellationToken)
    {
        if (online is null)
        {
            reporter.Announce("Database: not configured (offline mode).");
            return 0;
        }

        reporter.Progress("Checking database connectivity...");
        try
        {
            // A full introspection is the honest end-to-end probe: it exercises the same path plan/apply rely on.
            var schema = await online.GetSchema(schemaNames: null, cancellationToken);
            reporter.Success($"Database: connected ({schema.Schemas.Count} schema{(schema.Schemas.Count == 1 ? "" : "s")} visible).");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reporter.Warn($"Database: unreachable — {ex.Message}");
            return 1;
        }
    }

    private async Task<int> CheckStateStore(CancellationToken cancellationToken)
    {
        if (store is null)
        {
            reporter.Announce("State store: not configured (offline planning unavailable).");
            return 0;
        }

        reporter.Progress("Checking state store...");
        ReadOnlyMemory<byte>? snapshot;
        try
        {
            snapshot = await store.Read(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reporter.Warn($"State store: unreachable — {ex.Message}");
            return 1;
        }

        // A missing or empty payload is a bootstrap store — reachable, with nothing recorded yet — not a corruption.
        if (snapshot is null or { IsEmpty: true })
        {
            reporter.Success("State store: reachable (no state recorded yet).");
            return 0;
        }

        // Reachable is necessary but not sufficient — a payload we can't deserialize would break every plan, so prove
        // the recorded snapshot still round-trips through the serializer.
        try
        {
            serializer.Deserialize(snapshot.Value);
            reporter.Success("State store: reachable, recorded state is valid.");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reporter.Warn($"State store: reachable but the recorded state is unreadable — {ex.Message}");
            return 1;
        }
    }

    private async Task<int> CheckStateLock(CancellationToken cancellationToken)
    {
        reporter.Progress("Checking state lock...");
        try
        {
            // There is no read-only peek on IStateLock, so acquire-then-release is the probe: it doubles as a real
            // readiness signal ("could an apply take the lock right now?") and reverts immediately on dispose.
            await using var handle = await stateLock.Acquire(new StateLockRequest("doctor"), cancellationToken);
            reporter.Success("State lock: free (acquirable).");
            return 0;
        }
        catch (StateLockedException locked)
        {
            // A held lock is a state, not a misconfiguration — it may be a legitimately-running operation — so report
            // it for visibility without counting it as a failure.
            var holder = locked.ExistingLock is { } info
                ? $"{info.Who} (operation '{info.Operation}', since {info.CreatedUtc:u})"
                : "another operation";
            reporter.Warn($"State lock: held by {holder}.");
            return 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            reporter.Warn($"State lock: could not be checked — {ex.Message}");
            return 1;
        }
    }
}
