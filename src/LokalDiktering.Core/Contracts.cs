namespace LokalDiktering.Core;

public interface IAppPathService
{
    string AppRoot { get; }
    string DataRoot { get; }
    string DocumentsRoot { get; }
    string ModelsRoot { get; }
    string LogsRoot { get; }
    string TempRoot { get; }
    bool IsSynchronizedLocation { get; }
    string GetDocumentDirectory(Guid documentId);
    string EnsureContainedPath(string path, string allowedRoot);
}

public interface IDocumentRepository
{
    Task<IReadOnlyList<DocumentSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<LocalDocument> CreateAsync(string? title = null, CancellationToken cancellationToken = default);
    Task<LocalDocument> LoadAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task SaveAsync(Guid documentId, string title, DocumentContent content, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface IVersionService
{
    Task<VersionMetadata?> CommitAsync(
        Guid documentId,
        DocumentContent content,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VersionMetadata>> ListAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<DocumentContent> LoadAsync(
        Guid documentId,
        Guid versionId,
        CancellationToken cancellationToken = default);
}

public interface IRecordingRepository
{
    Task<IReadOnlyList<RecordingMetadata>> ListAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task SaveAsync(RecordingMetadata recording, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid documentId, Guid recordingId, CancellationToken cancellationToken = default);
}

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public interface IAudioDeviceService
{
    Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync(CancellationToken cancellationToken = default);
}

public interface IAudioRecorder : IAsyncDisposable
{
    bool IsRecording { get; }
    event EventHandler<RecordingProgress>? Progress;
    Task StartAsync(string deviceId, string partialFilePath, CancellationToken cancellationToken = default);
    void Pause();
    void Resume();
    Task<TimeSpan> StopAsync(string finalFilePath, CancellationToken cancellationToken = default);
    Task CancelAsync(CancellationToken cancellationToken = default);
}

public interface IAudioPlaybackService : IAsyncDisposable
{
    Task PlayAsync(string wavPath, CancellationToken cancellationToken = default);
    void Stop();
}

public interface IPartialRecordingRecovery
{
    IReadOnlyList<PartialRecording> Find();
    Task RecoverAsync(PartialRecording partial, CancellationToken cancellationToken = default);
    Task DeleteAsync(PartialRecording partial, CancellationToken cancellationToken = default);
}

public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken = default);
}

public interface ITextAssistantService : IAsyncDisposable
{
    Task<AssistantResult> ProcessAsync(AssistantRequest request, CancellationToken cancellationToken = default);
    Task UnloadAsync();
}

public interface IAssistantHistoryService
{
    Task<IReadOnlyList<AssistantHistoryEntry>> ListAsync(Guid documentId, CancellationToken cancellationToken = default);
    Task AddAsync(Guid documentId, AssistantHistoryEntry entry, CancellationToken cancellationToken = default);
    Task ClearAsync(Guid documentId, CancellationToken cancellationToken = default);
}

public interface IModelManifestValidator
{
    Task<IReadOnlyList<string>> ValidateAsync(CancellationToken cancellationToken = default);
}

public interface ILocalLog
{
    void Information(string eventId, Guid? documentId = null, Guid? recordingId = null, double? durationMs = null);
    void Error(string eventId, Exception exception, Guid? documentId = null, Guid? recordingId = null);
}
