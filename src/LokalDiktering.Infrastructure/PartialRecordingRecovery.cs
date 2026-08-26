using System.Text;
using LokalDiktering.Core;
using NAudio.Wave;

namespace LokalDiktering.Infrastructure;

public sealed class PartialRecordingRecovery(
    IAppPathService paths,
    IRecordingRepository recordings) : IPartialRecordingRecovery
{
    public IReadOnlyList<PartialRecording> Find()
    {
        if (!Directory.Exists(paths.DocumentsRoot))
        {
            return [];
        }

        var result = new List<PartialRecording>();
        foreach (var file in Directory.EnumerateFiles(
                     paths.DocumentsRoot,
                     "*.partial.wav",
                     SearchOption.AllDirectories))
        {
            var fullPath = paths.EnsureContainedPath(file, paths.DocumentsRoot);
            var recordingDirectory = Directory.GetParent(fullPath);
            var documentDirectory = recordingDirectory?.Parent;
            if (recordingDirectory is null ||
                documentDirectory is null ||
                !Guid.TryParse(documentDirectory.Name, out var documentId) ||
                !Guid.TryParse(Path.GetFileName(fullPath).Replace(".partial.wav", string.Empty, StringComparison.Ordinal), out var recordingId))
            {
                continue;
            }

            result.Add(new PartialRecording(documentId, recordingId, fullPath));
        }

        return result;
    }

    public async Task RecoverAsync(
        PartialRecording partial,
        CancellationToken cancellationToken = default)
    {
        paths.EnsureContainedPath(partial.Path, paths.DocumentsRoot);
        RepairWaveHeader(partial.Path);
        var finalPath = Path.Combine(Path.GetDirectoryName(partial.Path)!, $"{partial.RecordingId:D}.wav");
        var convertedPath = partial.Path + ".recovered";
        using (var reader = new AudioFileReader(partial.Path))
        using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(16_000, 16, 1)))
        {
            resampler.ResamplerQuality = 60;
            WaveFileWriter.CreateWaveFile(convertedPath, resampler);
        }

        File.Move(convertedPath, finalPath, true);
        File.Delete(partial.Path);
        using var recovered = new WaveFileReader(finalPath);
        await recordings.SaveAsync(
            new RecordingMetadata
            {
                Id = partial.RecordingId,
                DocumentId = partial.DocumentId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Duration = recovered.TotalTime,
                State = RecordingState.Ready
            },
            cancellationToken);
    }

    public Task DeleteAsync(PartialRecording partial, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        paths.EnsureContainedPath(partial.Path, paths.DocumentsRoot);
        File.Delete(partial.Path);
        return Task.CompletedTask;
    }

    private static void RepairWaveHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var reader = new BinaryReader(stream, Encoding.ASCII, true);
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF")
        {
            throw new InvalidDataException("Inspelningen saknar RIFF-huvud.");
        }

        stream.Position = 4;
        writer.Write(checked((int)(stream.Length - 8)));
        stream.Position = 12;
        while (stream.Position + 8 <= stream.Length)
        {
            var chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
            var sizePosition = stream.Position;
            var size = reader.ReadInt32();
            var dataPosition = stream.Position;
            if (chunkId == "data")
            {
                stream.Position = sizePosition;
                writer.Write(checked((int)(stream.Length - dataPosition)));
                writer.Flush();
                return;
            }

            stream.Position = Math.Min(stream.Length, dataPosition + size + (size % 2));
        }

        throw new InvalidDataException("Inspelningen saknar ljuddata.");
    }
}
