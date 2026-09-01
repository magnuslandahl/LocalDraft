using System.Diagnostics;
using LocalDraft.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace LocalDraft.Infrastructure;

public sealed class AudioDeviceService : IAudioDeviceService
{
    public Task<IReadOnlyList<AudioDevice>> GetInputDevicesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var enumerator = new MMDeviceEnumerator();
        var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
            .Select(x => new AudioDevice(x.ID, x.FriendlyName))
            .ToArray();
        return Task.FromResult<IReadOnlyList<AudioDevice>>(devices);
    }
}

public sealed class WasapiAudioRecorder : IAudioRecorder
{
    private readonly object sync = new();
    private WasapiCapture? capture;
    private WaveFileWriter? writer;
    private string? partialPath;
    private bool paused;
    private Stopwatch elapsed = new();
    private WaveFormat? captureFormat;

    public bool IsRecording => capture is not null;
    public event EventHandler<RecordingProgress>? Progress;

    public Task StartAsync(
        string deviceId,
        string partialFilePath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (capture is not null)
        {
            throw new InvalidOperationException("En inspelning pågår redan.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(partialFilePath)!);
        using var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDevice(deviceId);
        capture = new WasapiCapture(device);
        captureFormat = capture.WaveFormat;
        writer = new WaveFileWriter(partialFilePath, captureFormat);
        partialPath = partialFilePath;
        paused = false;
        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;
        capture.StartRecording();
        elapsed.Restart();
        return Task.CompletedTask;
    }

    public void Pause() => paused = true;
    public void Resume() => paused = false;

    public async Task<TimeSpan> StopAsync(
        string finalFilePath,
        CancellationToken cancellationToken = default)
    {
        var activeCapture = capture ?? throw new InvalidOperationException("Ingen inspelning pågår.");
        activeCapture.StopRecording();
        await WaitUntilStoppedAsync(cancellationToken);
        elapsed.Stop();

        var sourcePath = partialPath!;
        var convertedPath = sourcePath + ".converted";
        using (var reader = new AudioFileReader(sourcePath))
        using (var resampler = new MediaFoundationResampler(reader, new WaveFormat(16_000, 16, 1)))
        {
            resampler.ResamplerQuality = 60;
            WaveFileWriter.CreateWaveFile(convertedPath, resampler);
        }

        File.Move(convertedPath, finalFilePath, true);
        File.Delete(sourcePath);
        return elapsed.Elapsed;
    }

    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        if (capture is not null)
        {
            capture.StopRecording();
            await WaitUntilStoppedAsync(cancellationToken);
        }

        if (partialPath is { } path && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (capture is not null)
        {
            await CancelAsync();
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs args)
    {
        lock (sync)
        {
            if (paused || writer is null)
            {
                return;
            }

            writer.Write(args.Buffer, 0, args.BytesRecorded);
            writer.Flush();
            Progress?.Invoke(
                this,
                new RecordingProgress(elapsed.Elapsed, EstimateLevel(args.Buffer, args.BytesRecorded, captureFormat)));
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs args)
    {
        lock (sync)
        {
            writer?.Dispose();
            writer = null;
            capture?.Dispose();
            capture = null;
            Monitor.PulseAll(sync);
        }
    }

    private Task WaitUntilStoppedAsync(CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            lock (sync)
            {
                while (capture is not null)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(sync, 100);
                }
            }
        }, cancellationToken);

    private static float EstimateLevel(byte[] buffer, int count, WaveFormat? format)
    {
        if (format is null || count < 2)
        {
            return 0;
        }

        double maximum = 0;
        if (format.Encoding == WaveFormatEncoding.IeeeFloat && format.BitsPerSample == 32)
        {
            for (var index = 0; index + 3 < count; index += 4)
            {
                var sample = Math.Abs(BitConverter.ToSingle(buffer, index));
                if (!float.IsNaN(sample))
                {
                    maximum = Math.Max(maximum, sample);
                }
            }
        }
        else if (format.BitsPerSample == 16)
        {
            for (var index = 0; index + 1 < count; index += 2)
            {
                maximum = Math.Max(maximum, Math.Abs(BitConverter.ToInt16(buffer, index)) / 32768d);
            }
        }

        return (float)Math.Clamp(maximum, 0, 1);
    }
}

public sealed class AudioPlaybackService : IAudioPlaybackService
{
    private WaveOutEvent? output;
    private AudioFileReader? reader;

    public async Task PlayAsync(string wavPath, CancellationToken cancellationToken = default)
    {
        Stop();
        output = new WaveOutEvent();
        reader = new AudioFileReader(wavPath);
        output.Init(reader);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        output.PlaybackStopped += (_, _) => completion.TrySetResult();
        using var registration = cancellationToken.Register(() =>
        {
            Stop();
            completion.TrySetCanceled(cancellationToken);
        });
        output.Play();
        await completion.Task;
        Stop();
    }

    public void Stop()
    {
        output?.Stop();
        output?.Dispose();
        output = null;
        reader?.Dispose();
        reader = null;
    }

    public ValueTask DisposeAsync()
    {
        Stop();
        return ValueTask.CompletedTask;
    }
}
