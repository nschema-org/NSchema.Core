using Microsoft.Extensions.DependencyInjection;
using NSchema.Diagnostics;
using NSchema.Schema;
using NSchema.State;

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
    [FromKeyedServices(NSchemaKeys.OnlineSchemaProvider)]
    ISchemaProvider? online = null,
    ISchemaStateStore? store = null,
    IStateLock? stateLock = null
) : IDoctorOperation
{
    public async Task<Result> Execute(DoctorArguments arguments, CancellationToken cancellationToken = default)
    {
        reporter.Announce("Running diagnostics...");

        // Each probe contributes one diagnostic; the aggregated set is the result, carried back to the caller. A check
        // that touches infra still catches (Npgsql/IO/JSON genuinely throw) — but converts the failure to an
        // error-severity diagnostic rather than tallying or throwing, so every problem is surfaced together.
        var diagnostics = new List<Diagnostic>
        {
            await CheckDatabase(cancellationToken),
            await CheckStateStore(cancellationToken),
        };

        // The lock check only means anything when the configured backend actually provides a lock; otherwise there is
        // nothing to probe.
        if (stateLock is not null)
        {
            diagnostics.Add(await CheckStateLock(stateLock, cancellationToken));
        }

        var result = Result.From(diagnostics);
        if (result.IsSuccess)
        {
            reporter.Success("All checks passed.");
        }

        return result;
    }

    private async Task<Diagnostic> CheckDatabase(CancellationToken cancellationToken)
    {
        const string source = "database";
        if (online is null)
        {
            return Announce(Diagnostic.Info(source, "Database: not configured (offline mode)."));
        }

        reporter.Progress("Checking database connectivity...");
        try
        {
            // A full introspection is the honest end-to-end probe: it exercises the same path plan/apply rely on.
            var schema = await online.GetSchema(schemaNames: null, cancellationToken);
            return Healthy(Diagnostic.Info(source, $"Database: connected ({schema.Schemas.Count} schema{(schema.Schemas.Count == 1 ? "" : "s")} visible)."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(Diagnostic.Error(source, $"Database: unreachable — {ex.Message}"));
        }
    }

    private async Task<Diagnostic> CheckStateStore(CancellationToken cancellationToken)
    {
        const string source = "state-store";
        if (store is null)
        {
            return Announce(Diagnostic.Info(source, "State store: not configured (offline planning unavailable)."));
        }

        reporter.Progress("Checking state store...");
        ReadOnlyMemory<byte>? snapshot;
        try
        {
            snapshot = await store.Read(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(Diagnostic.Error(source, $"State store: unreachable — {ex.Message}"));
        }

        // A missing or empty payload is a bootstrap store — reachable, with nothing recorded yet — not a corruption.
        if (snapshot is null or { IsEmpty: true })
        {
            return Healthy(Diagnostic.Info(source, "State store: reachable (no state recorded yet)."));
        }

        // Reachable is necessary but not sufficient — a payload we can't deserialize would break every plan, so prove
        // the recorded snapshot still round-trips through the serializer.
        try
        {
            serializer.Deserialize(snapshot.Value);
            return Healthy(Diagnostic.Info(source, "State store: reachable, recorded state is valid."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(Diagnostic.Error(source, $"State store: reachable but the recorded state is unreadable — {ex.Message}"));
        }
    }

    private async Task<Diagnostic> CheckStateLock(IStateLock stateLock, CancellationToken cancellationToken)
    {
        const string source = "state-lock";
        reporter.Progress("Checking state lock...");
        try
        {
            var info = await stateLock.Peek(cancellationToken);
            return info is null
                ? Healthy(Diagnostic.Info(source, "State lock: free."))
                // A held lock is a state, not a misconfiguration — it may be a legitimately-running operation — so
                // report it for visibility (warning) without counting it as a failure.
                : Problem(Diagnostic.Warning(source, $"State lock: held by {info.Who} (operation '{info.Operation}', since {info.CreatedUtc:u})."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(Diagnostic.Error(source, $"State lock: could not be checked — {ex.Message}"));
        }
    }

    // Live narration mirrors each diagnostic's severity: healthy/announcement lines go to the positive channels,
    // warnings and errors to the warning channel. The diagnostic itself is what the result carries.
    private Diagnostic Announce(Diagnostic diagnostic)
    {
        reporter.Announce(diagnostic.Message);
        return diagnostic;
    }

    private Diagnostic Healthy(Diagnostic diagnostic)
    {
        reporter.Success(diagnostic.Message);
        return diagnostic;
    }

    private Diagnostic Problem(Diagnostic diagnostic)
    {
        reporter.Warn(diagnostic.Message);
        return diagnostic;
    }
}
