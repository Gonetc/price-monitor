using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PriceMonitor.Reporting;

/// <summary>Keeps the latest run on disk as JSON so the next run can detect changes.</summary>
public class SnapshotStore(IOptions<MonitorOptions> options)
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    private string FilePath => Path.Combine(Path.GetFullPath(options.Value.OutputFolder), "latest-snapshot.json");

    public async Task<Snapshot?> LoadLatestAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<Snapshot>(stream, Json, ct);
    }

    public async Task SaveAsync(Snapshot snapshot, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);

        // Write to a temp file first so a crash never leaves a half-written snapshot
        var tempPath = FilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, snapshot, Json, ct);
        }

        File.Move(tempPath, FilePath, overwrite: true);
    }
}
