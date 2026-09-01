using System.Text.Json;
using LocalDraft.Core;

namespace LocalDraft.Infrastructure;

public sealed class AssistantHistoryService(IAppPathService paths) : IAssistantHistoryService
{
    public async Task<IReadOnlyList<AssistantHistoryEntry>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var path = GetPath(documentId);
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<List<AssistantHistoryEntry>>(
                   stream,
                   AtomicFile.JsonOptions,
                   cancellationToken)
               ?? [];
    }

    public async Task AddAsync(
        Guid documentId,
        AssistantHistoryEntry entry,
        CancellationToken cancellationToken = default)
    {
        var history = (await ListAsync(documentId, cancellationToken)).ToList();
        history.Add(entry);
        await AtomicFile.WriteJsonAsync(GetPath(documentId), history, paths.DataRoot, cancellationToken);
    }

    public Task ClearAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(documentId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string GetPath(Guid documentId)
    {
        var directory = paths.EnsureContainedPath(
            Path.Combine(paths.GetDocumentDirectory(documentId), "assistant"),
            paths.DocumentsRoot);
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "history.json");
    }
}
