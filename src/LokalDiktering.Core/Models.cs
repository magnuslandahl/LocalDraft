using System.Text.Json.Serialization;

namespace LokalDiktering.Core;

public sealed record DocumentMetadata
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid Id { get; init; }
    public required string Title { get; set; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required DateTimeOffset ModifiedUtc { get; set; }
    public Guid? CurrentVersionId { get; set; }
    public required string ContentHash { get; set; }
}

public sealed record DocumentContent(string Rtf, string PlainText);

public sealed record LocalDocument(DocumentMetadata Metadata, DocumentContent Content);

public sealed record DocumentSummary(Guid Id, string Title, DateTimeOffset ModifiedUtc)
{
    [JsonIgnore]
    public DateTimeOffset ModifiedLocal => ModifiedUtc.ToLocalTime();
}

public sealed record VersionMetadata
{
    public required Guid Id { get; init; }
    public required Guid DocumentId { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string Reason { get; init; }
    public required string ContentHash { get; init; }
    public required string Preview { get; init; }

    [JsonIgnore]
    public DateTimeOffset CreatedLocal => CreatedUtc.ToLocalTime();
}

public enum RecordingState
{
    Ready,
    Transcribing,
    Transcribed,
    Failed
}

public sealed record RecordingMetadata
{
    public required Guid Id { get; init; }
    public required Guid DocumentId { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required TimeSpan Duration { get; set; }
    public RecordingState State { get; set; } = RecordingState.Ready;
    public string? FailureCategory { get; set; }

    [JsonIgnore]
    public DateTimeOffset CreatedLocal => CreatedUtc.ToLocalTime();

    [JsonIgnore]
    public string StateLabel => State switch
    {
        RecordingState.Ready => "Redo att transkribera",
        RecordingState.Transcribing => "Transkriberar",
        RecordingState.Transcribed => "Transkriberad",
        RecordingState.Failed => "Misslyckades",
        _ => "Okänd"
    };
}

public sealed record AudioDevice(string Id, string Name);

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;
    public string? SelectedMicrophoneId { get; set; }
    public bool FirstRunCompleted { get; set; }
    public int? CpuThreads { get; set; }
}

public sealed record RecordingProgress(TimeSpan Elapsed, float Level);

public sealed record PartialRecording(Guid DocumentId, Guid RecordingId, string Path);

public sealed record TranscriptionResult(string Text, TimeSpan ProcessingTime);

public enum AssistantAction
{
    Cleanup,
    Summarize,
    Structure,
    Improve,
    BulletList,
    Custom
}

public sealed record AssistantRequest(
    AssistantAction Action,
    string SourceText,
    string? CustomInstruction = null);

public sealed record AssistantResult(
    string Text,
    IReadOnlyList<string> MissingProtectedTokens,
    int GeneratedTokenCount = 0,
    TimeSpan ProcessingTime = default,
    TimeSpan FirstTokenLatency = default);

public sealed record AssistantHistoryEntry
{
    public required Guid Id { get; init; }
    public required DateTimeOffset CreatedUtc { get; init; }
    public required string Action { get; init; }
    public required string Result { get; init; }

    [JsonIgnore]
    public DateTimeOffset CreatedLocal => CreatedUtc.ToLocalTime();
}

public sealed record ModelManifest
{
    public int SchemaVersion { get; init; } = 1;
    public required IReadOnlyList<ModelManifestEntry> Models { get; init; }
}

public sealed record ModelManifestEntry
{
    public required string Role { get; init; }
    public required string RelativePath { get; init; }
    public required string Source { get; init; }
    public required string Revision { get; init; }
    public required string Sha256 { get; init; }
    public required long Size { get; init; }
    public required string License { get; init; }
}

[JsonSerializable(typeof(DocumentMetadata))]
[JsonSerializable(typeof(List<DocumentSummary>))]
[JsonSerializable(typeof(List<VersionMetadata>))]
[JsonSerializable(typeof(RecordingMetadata))]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<AssistantHistoryEntry>))]
[JsonSerializable(typeof(ModelManifest))]
public partial class StorageJsonContext : JsonSerializerContext;
