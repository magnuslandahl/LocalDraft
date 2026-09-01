using System.Text.Json;
using LocalDraft.Core;

namespace LocalDraft.Infrastructure;

public sealed class DocumentRepository(IAppPathService paths) : IDocumentRepository
{
    private static readonly string EmptyRtf = @"{\rtf1\ansi\deff0{\fonttbl{\f0 Segoe UI;}}\f0\fs22 }";

    public async Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(paths.DocumentsRoot))
        {
            return [];
        }

        var documents = new List<DocumentSummary>();
        foreach (var directory in Directory.EnumerateDirectories(paths.DocumentsRoot))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var metadataPath = Path.Combine(directory, "document.json");
            if (!File.Exists(metadataPath))
            {
                continue;
            }

            await using var stream = File.OpenRead(metadataPath);
            var metadata = await JsonSerializer.DeserializeAsync<DocumentMetadata>(
                stream,
                AtomicFile.JsonOptions,
                cancellationToken);
            if (metadata is not null)
            {
                documents.Add(new DocumentSummary(metadata.Id, metadata.Title, metadata.ModifiedUtc));
            }
        }

        return documents.OrderByDescending(x => x.ModifiedUtc).ToArray();
    }

    public async Task<LocalDocument> CreateAsync(
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var localNow = now.ToLocalTime();
        var documentTitle = title ?? $"Nytt dokument – {localNow:dd MMMM yyyy HH.mm}";
        var content = new DocumentContent(EmptyRtf, string.Empty);
        var metadata = new DocumentMetadata
        {
            Id = id,
            Title = documentTitle,
            CreatedUtc = now,
            ModifiedUtc = now,
            ContentHash = ContentHash.Compute(content.Rtf, content.PlainText)
        };

        Directory.CreateDirectory(paths.GetDocumentDirectory(id));
        await WriteDocumentAsync(metadata, content, cancellationToken);
        return new LocalDocument(metadata, content);
    }

    public async Task<LocalDocument> LoadAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var directory = paths.GetDocumentDirectory(documentId);
        await using var metadataStream = File.OpenRead(Path.Combine(directory, "document.json"));
        var metadata = await JsonSerializer.DeserializeAsync<DocumentMetadata>(
                metadataStream,
                AtomicFile.JsonOptions,
                cancellationToken)
            ?? throw new InvalidDataException("Dokumentets metadata kan inte läsas.");
        var rtf = await File.ReadAllTextAsync(Path.Combine(directory, "current.rtf"), cancellationToken);
        var plainText = await File.ReadAllTextAsync(Path.Combine(directory, "current.txt"), cancellationToken);
        return new LocalDocument(metadata, new DocumentContent(rtf, plainText));
    }

    public async Task SaveAsync(
        Guid documentId,
        string title,
        DocumentContent content,
        CancellationToken cancellationToken = default)
    {
        var current = await LoadAsync(documentId, cancellationToken);
        current.Metadata.Title = title.Trim();
        current.Metadata.ModifiedUtc = DateTimeOffset.UtcNow;
        current.Metadata.ContentHash = ContentHash.Compute(content.Rtf, content.PlainText);
        await WriteDocumentAsync(current.Metadata, content, cancellationToken);
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        var directory = paths.GetDocumentDirectory(documentId);
        if (!Directory.Exists(directory))
        {
            return;
        }

        ValidateNoReparsePoints(directory);
        await Task.Run(() => Directory.Delete(directory, true), cancellationToken);
        if (Directory.Exists(directory))
        {
            throw new IOException("Dokumentmappen kunde inte tas bort helt.");
        }
    }

    private async Task WriteDocumentAsync(
        DocumentMetadata metadata,
        DocumentContent content,
        CancellationToken cancellationToken)
    {
        var directory = paths.GetDocumentDirectory(metadata.Id);
        Directory.CreateDirectory(directory);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "current.rtf"),
            content.Rtf,
            paths.DataRoot,
            cancellationToken);
        await AtomicFile.WriteTextAsync(
            Path.Combine(directory, "current.txt"),
            content.PlainText,
            paths.DataRoot,
            cancellationToken);
        await AtomicFile.WriteJsonAsync(
            Path.Combine(directory, "document.json"),
            metadata,
            paths.DataRoot,
            cancellationToken);
    }

    private static void ValidateNoReparsePoints(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories)
                     .Prepend(directory))
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Dokumentmappen innehåller en länk och kan inte tas bort säkert.");
            }
        }
    }
}

public sealed class VersionService(IAppPathService paths) : IVersionService
{
    public async Task<VersionMetadata?> CommitAsync(
        Guid documentId,
        DocumentContent content,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var versions = await ListAsync(documentId, cancellationToken);
        var hash = ContentHash.Compute(content.Rtf, content.PlainText);
        if (versions.FirstOrDefault()?.ContentHash == hash)
        {
            return null;
        }

        var id = Guid.NewGuid();
        var metadata = new VersionMetadata
        {
            Id = id,
            DocumentId = documentId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Reason = reason,
            ContentHash = hash,
            Preview = CreatePreview(content.PlainText)
        };
        var directory = GetVersionsDirectory(documentId);
        Directory.CreateDirectory(directory);
        await AtomicFile.WriteTextAsync(Path.Combine(directory, $"{id:D}.rtf"), content.Rtf, paths.DataRoot, cancellationToken);
        await AtomicFile.WriteTextAsync(Path.Combine(directory, $"{id:D}.txt"), content.PlainText, paths.DataRoot, cancellationToken);
        await AtomicFile.WriteJsonAsync(Path.Combine(directory, $"{id:D}.json"), metadata, paths.DataRoot, cancellationToken);
        return metadata;
    }

    public async Task<IReadOnlyList<VersionMetadata>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var directory = GetVersionsDirectory(documentId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var result = new List<VersionMetadata>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var item = await JsonSerializer.DeserializeAsync<VersionMetadata>(
                stream,
                AtomicFile.JsonOptions,
                cancellationToken);
            if (item is not null)
            {
                result.Add(item);
            }
        }

        return result.OrderByDescending(x => x.CreatedUtc).ToArray();
    }

    public async Task<DocumentContent> LoadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var directory = GetVersionsDirectory(documentId);
        var rtf = await File.ReadAllTextAsync(Path.Combine(directory, $"{versionId:D}.rtf"), cancellationToken);
        var text = await File.ReadAllTextAsync(Path.Combine(directory, $"{versionId:D}.txt"), cancellationToken);
        return new DocumentContent(rtf, text);
    }

    private string GetVersionsDirectory(Guid documentId) =>
        paths.EnsureContainedPath(
            Path.Combine(paths.GetDocumentDirectory(documentId), "versions"),
            paths.DocumentsRoot);

    private static string CreatePreview(string text)
    {
        var normalized = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= 120 ? normalized : normalized[..117] + "...";
    }
}

public sealed class RecordingRepository(IAppPathService paths) : IRecordingRepository
{
    public async Task<IReadOnlyList<RecordingMetadata>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var directory = GetDirectory(documentId);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var items = new List<RecordingMetadata>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            var item = await JsonSerializer.DeserializeAsync<RecordingMetadata>(
                stream,
                AtomicFile.JsonOptions,
                cancellationToken);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items.OrderByDescending(x => x.CreatedUtc).ToArray();
    }

    public Task SaveAsync(RecordingMetadata recording, CancellationToken cancellationToken = default)
    {
        var directory = GetDirectory(recording.DocumentId);
        Directory.CreateDirectory(directory);
        return AtomicFile.WriteJsonAsync(
            Path.Combine(directory, $"{recording.Id:D}.json"),
            recording,
            paths.DataRoot,
            cancellationToken);
    }

    public Task DeleteAsync(
        Guid documentId,
        Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        var directory = GetDirectory(documentId);
        foreach (var extension in new[] { ".wav", ".json", ".partial.wav" })
        {
            var file = paths.EnsureContainedPath(Path.Combine(directory, recordingId.ToString("D") + extension), directory);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }

        return Task.CompletedTask;
    }

    private string GetDirectory(Guid documentId) =>
        paths.EnsureContainedPath(
            Path.Combine(paths.GetDocumentDirectory(documentId), "recordings"),
            paths.DocumentsRoot);
}
