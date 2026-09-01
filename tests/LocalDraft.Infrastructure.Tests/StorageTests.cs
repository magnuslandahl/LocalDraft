using System.Text.Json;
using LocalDraft.Core;
using LocalDraft.Infrastructure;

namespace LocalDraft.Infrastructure.Tests;

public sealed class StorageTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "LocalDraftTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void PathService_RejectsEscapingAllowedRoot()
    {
        var paths = new AppPathService(root);
        Assert.Throws<InvalidOperationException>(() =>
            paths.EnsureContainedPath(Path.Combine(root, "..", "outside.txt"), paths.DataRoot));
    }

    [Fact]
    public async Task Repository_PersistsRtfAndPlainTextAtomically()
    {
        var paths = new AppPathService(root);
        var repository = new DocumentRepository(paths);
        var created = await repository.CreateAsync("Test");
        var content = new DocumentContent(@"{\rtf1\ansi Hej}", "Hej");

        await repository.SaveAsync(created.Metadata.Id, "Ny titel", content);
        var loaded = await repository.LoadAsync(created.Metadata.Id);

        Assert.Equal("Ny titel", loaded.Metadata.Title);
        Assert.Equal(content, loaded.Content);
        Assert.Empty(Directory.EnumerateFiles(paths.DataRoot, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Versions_DeduplicateAndRestoreContent()
    {
        var paths = new AppPathService(root);
        var repository = new DocumentRepository(paths);
        var versions = new VersionService(paths);
        var document = await repository.CreateAsync();
        var content = new DocumentContent(@"{\rtf1 A}", "A");

        var first = await versions.CommitAsync(document.Metadata.Id, content, "Manuell redigering");
        var duplicate = await versions.CommitAsync(document.Metadata.Id, content, "Manuell redigering");
        var restored = await versions.LoadAsync(document.Metadata.Id, first!.Id);

        Assert.NotNull(first);
        Assert.Null(duplicate);
        Assert.Equal(content, restored);
        Assert.Single(await versions.ListAsync(document.Metadata.Id));
    }

    [Fact]
    public async Task DeletingDocument_RemovesCompleteDirectory()
    {
        var paths = new AppPathService(root);
        var repository = new DocumentRepository(paths);
        var document = await repository.CreateAsync();
        var directory = paths.GetDocumentDirectory(document.Metadata.Id);
        Directory.CreateDirectory(Path.Combine(directory, "recordings"));
        await File.WriteAllTextAsync(Path.Combine(directory, "recordings", "sample.wav"), "test");

        await repository.DeleteAsync(document.Metadata.Id);

        Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task RecordingDeletion_RemovesAudioAndMetadata()
    {
        var paths = new AppPathService(root);
        var documents = new DocumentRepository(paths);
        var repository = new RecordingRepository(paths);
        var document = await documents.CreateAsync();
        var recording = new RecordingMetadata
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Metadata.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromSeconds(2)
        };
        await repository.SaveAsync(recording);
        var wav = Path.Combine(paths.GetDocumentDirectory(document.Metadata.Id), "recordings", $"{recording.Id:D}.wav");
        await File.WriteAllTextAsync(wav, "wav");

        await repository.DeleteAsync(document.Metadata.Id, recording.Id);

        Assert.False(File.Exists(wav));
        Assert.Empty(await repository.ListAsync(document.Metadata.Id));
    }

    [Fact]
    public async Task ManifestValidator_FailsClosedOnHashMismatch()
    {
        var paths = new AppPathService(root);
        Directory.CreateDirectory(paths.ModelsRoot);
        var modelPath = Path.Combine(paths.ModelsRoot, "model.bin");
        await File.WriteAllTextAsync(modelPath, "wrong");
        var manifest = new ModelManifest
        {
            Models =
            [
                new ModelManifestEntry
                {
                    Role = "test",
                    RelativePath = "model.bin",
                    Source = "local",
                    Revision = "1",
                    Sha256 = new string('0', 64),
                    Size = new FileInfo(modelPath).Length,
                    License = "MIT"
                }
            ]
        };
        await File.WriteAllTextAsync(
            Path.Combine(paths.ModelsRoot, "manifest.json"),
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        var errors = await new ModelManifestValidator(paths).ValidateAsync();

        Assert.Contains(errors, error => error.Contains("skadad", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task PartialRecording_IsRecoveredToFinalWav()
    {
        var paths = new AppPathService(root);
        var documents = new DocumentRepository(paths);
        var recordings = new RecordingRepository(paths);
        var document = await documents.CreateAsync();
        var recordingId = Guid.NewGuid();
        var recordingDirectory = Path.Combine(paths.GetDocumentDirectory(document.Metadata.Id), "recordings");
        Directory.CreateDirectory(recordingDirectory);
        var partial = Path.Combine(recordingDirectory, $"{recordingId:D}.partial.wav");
        File.Copy(
            Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "svenska-test.wav"),
            partial);
        var bytes = await File.ReadAllBytesAsync(partial);
        var dataOffset = FindSequence(bytes, "data"u8);
        await using (var stream = new FileStream(partial, FileMode.Open, FileAccess.Write, FileShare.None))
        using (var writer = new BinaryWriter(stream))
        {
            stream.Position = 4;
            writer.Write(0);
            stream.Position = dataOffset + 4;
            writer.Write(0);
        }

        var service = new PartialRecordingRecovery(paths, recordings);
        var found = Assert.Single(service.Find());
        await service.RecoverAsync(found);

        Assert.False(File.Exists(partial));
        Assert.True(File.Exists(Path.Combine(recordingDirectory, $"{recordingId:D}.wav")));
        Assert.Single(await recordings.ListAsync(document.Metadata.Id));
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LocalDraft.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }

    private static int FindSequence(byte[] bytes, ReadOnlySpan<byte> sequence)
    {
        for (var index = 0; index <= bytes.Length - sequence.Length; index++)
        {
            if (bytes.AsSpan(index, sequence.Length).SequenceEqual(sequence))
            {
                return index;
            }
        }
        throw new InvalidDataException("Sekvensen hittades inte.");
    }
}
