using LokalDiktering.Infrastructure;
using LokalDiktering.Core;
using NAudio.Wave;
using System.Text.Json;

namespace LokalDiktering.Infrastructure.Tests;

public sealed class WhisperIntegrationTests
{
    [Fact]
    public void SwedishFixture_IsPcm16Khz16BitMono()
    {
        var fixture = Path.Combine(FindRepositoryRoot(), "tests", "fixtures", "svenska-test.wav");
        using var reader = new WaveFileReader(fixture);
        Assert.Equal(16_000, reader.WaveFormat.SampleRate);
        Assert.Equal(16, reader.WaveFormat.BitsPerSample);
        Assert.Equal(1, reader.WaveFormat.Channels);
        Assert.Equal(WaveFormatEncoding.Pcm, reader.WaveFormat.Encoding);
    }

    [Fact]
    public async Task Whisper_TranscribesSwedishFixtureOffline_WhenAssetsAreAvailable()
    {
        var root = FindRepositoryRoot();
        var helper = Path.Combine(root, "Native", "Whisper", "whisper-cli.exe");
        var model = Path.Combine(root, "Models", "Whisper", "ggml-small-q5_1.bin");
        if (!File.Exists(helper) || !File.Exists(model))
        {
            return;
        }

        var paths = new AppPathService(root);
        var wav = Path.Combine(paths.TempRoot, "swedish-integration.wav");
        File.Copy(Path.Combine(root, "tests", "fixtures", "svenska-test.wav"), wav, true);
        var service = new WhisperCliTranscriptionService(paths, new LocalLog(paths));

        var result = await service.TranscribeAsync(wav);

        Assert.Contains("lokalt", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("svensk", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(wav));
        Assert.Empty(Directory.EnumerateFiles(paths.TempRoot, "whisper-*.json"));
    }

    [Fact]
    public async Task TextModel_ProcessesSwedishAndPreservesProtectedValues_WhenAssetIsAvailable()
    {
        var root = FindRepositoryRoot();
        var model = Path.Combine(root, "Models", "Text", "Qwen3-1.7B-Q4_K_M.gguf");
        if (!File.Exists(model))
        {
            return;
        }

        var paths = new AppPathService(root);
        await using var service = new LlamaTextAssistantService(paths);
        const string source = "Eh, Åsa Lindström kommer klockan 09.30 och beloppet är 128 450 kronor.";

        var result = await service.ProcessAsync(new AssistantRequest(AssistantAction.Cleanup, source));

        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.Contains("Åsa Lindström", result.Text, StringComparison.Ordinal);
        Assert.Contains("09.30", result.Text, StringComparison.Ordinal);
        Assert.Contains("128 450 kronor", result.Text, StringComparison.Ordinal);
        Assert.Empty(result.MissingProtectedTokens);
    }

    [Fact]
    public async Task TextModel_PassesTwelveCaseSwedishAcceptanceCorpus_WhenAssetIsAvailable()
    {
        var root = FindRepositoryRoot();
        var model = Path.Combine(root, "Models", "Text", "Qwen3-1.7B-Q4_K_M.gguf");
        if (!File.Exists(model))
        {
            return;
        }

        using var corpus = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(root, "tests", "fixtures", "swedish-assistant-corpus.json")));
        await using var service = new LlamaTextAssistantService(new AppPathService(root));
        var failures = new List<string>();
        foreach (var item in corpus.RootElement.EnumerateArray())
        {
            var actionName = item.GetProperty("action").GetString()!;
            var action = Enum.Parse<AssistantAction>(actionName);
            var source = item.GetProperty("source").GetString()!;
            var instruction = item.TryGetProperty("instruction", out var instructionElement)
                ? instructionElement.GetString()
                : null;
            var result = await service.ProcessAsync(new AssistantRequest(action, source, instruction));
            if (string.IsNullOrWhiteSpace(result.Text) ||
                result.Text.Contains("<think>", StringComparison.OrdinalIgnoreCase) ||
                result.Text.StartsWith("Här är", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{actionName}: ogiltigt svar");
            }

            foreach (var protectedToken in item.GetProperty("protected").EnumerateArray()
                         .Select(x => x.GetString()!))
            {
                if (!result.Text.Contains(protectedToken, StringComparison.Ordinal))
                {
                    failures.Add($"{actionName}: saknar {protectedToken}");
                }
            }
        }

        Assert.Empty(failures);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LokalDiktering.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException();
    }
}
