using System.Text.Json;
using LokalDiktering.Core;

namespace LokalDiktering.Infrastructure;

public sealed class AppPathService : IAppPathService
{
    private static readonly string[] SyncMarkers =
        ["OneDrive", "Dropbox", "Google Drive", "iCloudDrive", "Box"];

    public AppPathService(string? appRoot = null)
    {
        AppRoot = Normalize(appRoot ?? AppContext.BaseDirectory);
        DataRoot = Path.Combine(AppRoot, "Data");
        DocumentsRoot = Path.Combine(DataRoot, "Documents");
        ModelsRoot = Path.Combine(AppRoot, "Models");
        LogsRoot = Path.Combine(DataRoot, "Logs");
        TempRoot = Path.Combine(DataRoot, "Temp");

        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(DocumentsRoot);
        Directory.CreateDirectory(Path.Combine(DataRoot, "Settings"));
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(TempRoot);

        IsSynchronizedLocation = SyncMarkers.Any(marker =>
            AppRoot.Contains(marker, StringComparison.OrdinalIgnoreCase)) ||
            AppRoot.StartsWith(@"\\", StringComparison.Ordinal);
    }

    public string AppRoot { get; }
    public string DataRoot { get; }
    public string DocumentsRoot { get; }
    public string ModelsRoot { get; }
    public string LogsRoot { get; }
    public string TempRoot { get; }
    public bool IsSynchronizedLocation { get; }

    public string GetDocumentDirectory(Guid documentId) =>
        EnsureContainedPath(Path.Combine(DocumentsRoot, documentId.ToString("D")), DocumentsRoot);

    public string EnsureContainedPath(string path, string allowedRoot)
    {
        var normalizedPath = Normalize(path);
        var normalizedRoot = Normalize(allowedRoot);
        var prefix = normalizedRoot.EndsWith(Path.DirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;

        if (!normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalizedPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Sökvägen ligger utanför appens tillåtna lagringsplats.");
        }

        return normalizedPath;
    }

    public static void ConfigureProcessEnvironment(string appRoot)
    {
        var root = Normalize(appRoot);
        var dataRoot = Path.Combine(root, "Data");
        var tempRoot = Path.Combine(dataRoot, "Temp");
        var cacheRoot = Path.Combine(dataRoot, "Cache");
        Directory.SetCurrentDirectory(root);
        Directory.CreateDirectory(tempRoot);
        Directory.CreateDirectory(cacheRoot);
        foreach (var variable in new[] { "TEMP", "TMP" })
        {
            Environment.SetEnvironmentVariable(variable, tempRoot, EnvironmentVariableTarget.Process);
        }

        foreach (var variable in new[] { "HF_HOME", "XDG_CACHE_HOME", "LLAMA_CACHE", "GGML_CACHE" })
        {
            Environment.SetEnvironmentVariable(variable, cacheRoot, EnvironmentVariableTarget.Process);
        }
    }

    public static void VerifyWritable(string appRoot)
    {
        var probe = Path.Combine(appRoot, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            throw new AppRootNotWritableException(exception);
        }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

public sealed class AppRootNotWritableException(Exception innerException)
    : IOException("Appens mapp är inte skrivbar.", innerException);

internal static class AtomicFile
{
    public static async Task WriteTextAsync(
        string targetPath,
        string content,
        string dataRoot,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("Filen saknar överordnad mapp.");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
        EnsureContained(tempPath, dataRoot);
        await File.WriteAllTextAsync(tempPath, content, cancellationToken);

        if (File.Exists(targetPath))
        {
            File.Move(tempPath, targetPath, true);
        }
        else
        {
            File.Move(tempPath, targetPath);
        }
    }

    public static Task WriteJsonAsync<T>(
        string targetPath,
        T value,
        string dataRoot,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value, JsonOptions);
        return WriteTextAsync(targetPath, json, dataRoot, cancellationToken);
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static void EnsureContained(string path, string root)
    {
        var fullPath = Path.GetFullPath(path);
        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Atomär skrivning utanför Data är inte tillåten.");
        }
    }
}
