namespace NSchema.Plan.PlanFile;

/// <summary>
/// Reads and writes saved plan files.
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
