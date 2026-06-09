namespace NSchema.Plan.PlanFile;

/// <summary>
/// Shared helper that writes a saved plan file. Used by the plan and plan-destroy operations so the
/// dialect requirement, envelope construction, and file write live in one place.
/// </summary>
internal class PlanFileWriter : IPlanFileWriter
{
    private readonly PlanFileSerializer _serializer = new();

    public async Task<PlanFileEnvelope> Read(string path, CancellationToken cancellationToken)
    {
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var envelope = _serializer.Deserialize(bytes);
        return envelope;
    }

    public async Task Write(string path, PlanFileEnvelope envelope, CancellationToken cancellationToken)
    {
        var bytes = _serializer.Serialize(envelope);
        await File.WriteAllBytesAsync(path, bytes.ToArray(), cancellationToken);
    }
}
