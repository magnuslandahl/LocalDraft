using System.Security.Cryptography;
using System.Text.Json;
using LokalDiktering.Core;

namespace LokalDiktering.Infrastructure;

public sealed class LocalLog(IAppPathService paths) : ILocalLog
{
    private readonly object sync = new();

    public void Information(
        string eventId,
        Guid? documentId = null,
        Guid? recordingId = null,
        double? durationMs = null) =>
        Write("information", eventId, documentId, recordingId, durationMs, null);

    public void Error(
        string eventId,
        Exception exception,
        Guid? documentId = null,
        Guid? recordingId = null) =>
        Write("error", eventId, documentId, recordingId, null, exception.GetType().FullName);

    private void Write(
        string level,
        string eventId,
        Guid? documentId,
        Guid? recordingId,
        double? durationMs,
        string? exceptionType)
    {
        var entry = JsonSerializer.Serialize(new
        {
            timestamp = DateTimeOffset.UtcNow,
            level,
            eventId,
            documentId,
            recordingId,
            durationMs,
            exceptionType
        });
        var path = Path.Combine(paths.LogsRoot, $"app-{DateTime.UtcNow:yyyyMMdd}.log");
        lock (sync)
        {
            Directory.CreateDirectory(paths.LogsRoot);
            File.AppendAllText(path, entry + Environment.NewLine);
            Rotate();
        }
    }

    private void Rotate()
    {
        const long maxTotalBytes = 10 * 1024 * 1024;
        var files = new DirectoryInfo(paths.LogsRoot).GetFiles("app-*.log")
            .OrderByDescending(x => x.LastWriteTimeUtc)
            .ToArray();
        long total = 0;
        foreach (var file in files)
        {
            total += file.Length;
            if (total > maxTotalBytes)
            {
                file.Delete();
            }
        }
    }
}

public sealed class ModelManifestValidator(IAppPathService paths) : IModelManifestValidator
{
    public async Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var manifestPath = Path.Combine(paths.ModelsRoot, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return ["Modellmanifestet saknas."];
        }

        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync<ModelManifest>(
            stream,
            AtomicFile.JsonOptions,
            cancellationToken);
        if (manifest is null)
        {
            return ["Modellmanifestet kan inte läsas."];
        }

        var errors = new List<string>();
        foreach (var model in manifest.Models)
        {
            var path = Path.GetFullPath(Path.Combine(paths.ModelsRoot, model.RelativePath));
            try
            {
                paths.EnsureContainedPath(path, paths.ModelsRoot);
            }
            catch (InvalidOperationException)
            {
                errors.Add($"Modellen {model.Role} har en otillåten sökväg.");
                continue;
            }

            if (!File.Exists(path))
            {
                errors.Add($"Modellen {model.Role} saknas.");
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length != model.Size)
            {
                errors.Add($"Modellen {model.Role} har fel storlek.");
                continue;
            }

            await using var modelStream = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(modelStream, cancellationToken))
                .ToLowerInvariant();
            if (!string.Equals(actual, model.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Modellen {model.Role} är skadad.");
            }
        }

        return errors;
    }
}
