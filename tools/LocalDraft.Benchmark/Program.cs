using System.Diagnostics;
using System.Text.Json;
using LocalDraft.Core;
using LocalDraft.Infrastructure;
using NAudio.Wave;

var root = args.Length > 0
    ? Path.GetFullPath(args[0])
    : throw new ArgumentException("Ange repositoryroten som första argument.");
var paths = new AppPathService(root);
var fixtureSource = Path.Combine(root, "tests", "fixtures", "svenska-test.wav");
var fixture = Path.Combine(paths.TempRoot, "benchmark-swedish-one-minute.wav");
using (var sourceReader = new WaveFileReader(fixtureSource))
using (var writer = new WaveFileWriter(fixture, sourceReader.WaveFormat))
{
    var repetitions = (int)Math.Ceiling(60 / sourceReader.TotalTime.TotalSeconds);
    var buffer = new byte[16_384];
    for (var repetition = 0; repetition < repetitions; repetition++)
    {
        sourceReader.Position = 0;
        int read;
        while ((read = sourceReader.Read(buffer, 0, buffer.Length)) > 0)
        {
            writer.Write(buffer, 0, read);
        }
    }
}
using var fixtureReader = new WaveFileReader(fixture);
var fixtureDuration = fixtureReader.TotalTime;

var whisper = new WhisperCliTranscriptionService(paths, new LocalLog(paths));
var (transcription, whisperPeak) = await MeasurePeakAsync(
    () => whisper.TranscribeAsync(fixture),
    "whisper-cli");

await using var assistant = new LlamaTextAssistantService(paths);
const string source = "Eh, Åsa Lindström kommer klockan 09.30 och beloppet är 128 450 kronor.";
var (assistantResult, assistantPeak) = await MeasurePeakAsync(
    () => assistant.ProcessAsync(new AssistantRequest(AssistantAction.Cleanup, source)));

var result = new
{
    timestampUtc = DateTimeOffset.UtcNow,
    machine = new
    {
        processorCount = Environment.ProcessorCount,
        os = Environment.OSVersion.VersionString
    },
    whisper = new
    {
        audioSeconds = fixtureDuration.TotalSeconds,
        processingSeconds = transcription.ProcessingTime.TotalSeconds,
        realtimeFactor = transcription.ProcessingTime.TotalSeconds / fixtureDuration.TotalSeconds,
        peakWorkingSetBytes = whisperPeak
    },
    textModel = new
    {
        processingSeconds = assistantResult.ProcessingTime.TotalSeconds,
        firstTokenSeconds = assistantResult.FirstTokenLatency.TotalSeconds,
        generatedTokens = assistantResult.GeneratedTokenCount,
        tokensPerSecond = assistantResult.GeneratedTokenCount / assistantResult.ProcessingTime.TotalSeconds,
        peakWorkingSetBytes = assistantPeak,
        protectedTokensPreserved = assistantResult.MissingProtectedTokens.Count == 0
    }
};

var output = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
var outputPath = Path.Combine(root, "artifacts", "benchmark-results.json");
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
await File.WriteAllTextAsync(outputPath, output);
Console.WriteLine(output);

static async Task<(T Result, long PeakWorkingSet)> MeasurePeakAsync<T>(
    Func<Task<T>> operation,
    string? childProcessName = null)
{
    var process = Process.GetCurrentProcess();
    var peak = process.WorkingSet64;
    using var cancellation = new CancellationTokenSource();
    var sampler = Task.Run(async () =>
    {
        while (!cancellation.IsCancellationRequested)
        {
            process.Refresh();
            var current = process.WorkingSet64;
            if (childProcessName is not null)
            {
                foreach (var child in Process.GetProcessesByName(childProcessName))
                {
                    using (child)
                    {
                        current += child.WorkingSet64;
                    }
                }
            }
            peak = Math.Max(peak, current);
            await Task.Delay(25, CancellationToken.None);
        }
    });

    try
    {
        return (await operation(), peak);
    }
    finally
    {
        cancellation.Cancel();
        await sampler;
    }
}
