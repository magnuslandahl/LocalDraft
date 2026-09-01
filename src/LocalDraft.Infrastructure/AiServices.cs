using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LLama;
using LLama.Common;
using LLama.Sampling;
using LocalDraft.Core;

namespace LocalDraft.Infrastructure;

public sealed class WhisperCliTranscriptionService(IAppPathService paths, ILocalLog log)
    : ITranscriptionService
{
    public async Task<TranscriptionResult> TranscribeAsync(
        string wavPath,
        CancellationToken cancellationToken = default)
    {
        var helper = Path.Combine(paths.AppRoot, "Native", "Whisper", "whisper-cli.exe");
        var model = Path.Combine(paths.ModelsRoot, "Whisper", "ggml-small-q5_1.bin");
        if (!File.Exists(helper) || !File.Exists(model))
        {
            throw new FileNotFoundException("Whisper-komponenten eller talmodellen saknas.");
        }

        paths.EnsureContainedPath(wavPath, paths.DataRoot);
        var outputBase = Path.Combine(paths.TempRoot, $"whisper-{Guid.NewGuid():N}");
        var outputJson = outputBase + ".json";
        var start = Stopwatch.StartNew();
        var processStart = new ProcessStartInfo(helper)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = paths.TempRoot
        };
        processStart.ArgumentList.Add("-m");
        processStart.ArgumentList.Add(model);
        processStart.ArgumentList.Add("-f");
        processStart.ArgumentList.Add(wavPath);
        processStart.ArgumentList.Add("-l");
        processStart.ArgumentList.Add("sv");
        processStart.ArgumentList.Add("-oj");
        processStart.ArgumentList.Add("-of");
        processStart.ArgumentList.Add(outputBase);
        processStart.ArgumentList.Add("-t");
        processStart.ArgumentList.Add(Math.Max(1, Environment.ProcessorCount - 1).ToString());

        using var process = Process.Start(processStart)
            ?? throw new InvalidOperationException("Kunde inte starta den lokala transkriberingen.");
        using var registration = cancellationToken.Register(() =>
        {
            if (!process.HasExited)
            {
                process.Kill(true);
            }
        });
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        await standardOutputTask;
        var standardError = await standardErrorTask;
        if (process.ExitCode != 0)
        {
            log.Error("whisper.failed", new InvalidOperationException($"ExitCode:{process.ExitCode}"));
            throw new InvalidOperationException("Transkriberingen misslyckades. Inspelningen finns kvar och kan provas igen.");
        }

        try
        {
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(outputJson, cancellationToken));
            var text = string.Join(
                " ",
                json.RootElement.GetProperty("transcription").EnumerateArray()
                    .Select(x => x.GetProperty("text").GetString()?.Trim())
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
            return new TranscriptionResult(text.Trim(), start.Elapsed);
        }
        finally
        {
            if (File.Exists(outputJson))
            {
                File.Delete(outputJson);
            }
        }
    }
}

public sealed class LlamaTextAssistantService(IAppPathService paths) : ITextAssistantService
{
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private LLamaWeights? weights;
    private ModelParams? modelParameters;

    public async Task<AssistantResult> ProcessAsync(
        AssistantRequest request,
        CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        var processing = Stopwatch.StartNew();
        var generatedTokenCount = 0;
        var firstTokenLatency = TimeSpan.Zero;
        try
        {
            await EnsureLoadedAsync(cancellationToken);
            var chunks = ParagraphChunker.Split(request.SourceText);
            var results = new List<string>(chunks.Count);
            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var prompt = AssistantPrompts.BuildUserMessage(request, chunk);
                var output = await GenerateAsync(prompt);
                if (request.Action != AssistantAction.Summarize)
                {
                    var missingInChunk = ProtectedTokenExtractor.FindMissing(chunk, output);
                    if (missingInChunk.Count > 0)
                    {
                        var requiredTokens = string.Join(
                            Environment.NewLine,
                            missingInChunk.Select(token => $"- {token}"));
                        output = await GenerateAsync(
                            $"{prompt}\n\nDet förra förslaget saknade exakta värden. Gör om hela uppgiften och se till att följande textdelar finns med exakt oförändrade:\n{requiredTokens}");
                    }
                }

                results.Add(output);
            }

            var combined = string.Join(Environment.NewLine + Environment.NewLine, results);
            var missing = request.Action == AssistantAction.Summarize
                ? []
                : ProtectedTokenExtractor.FindMissing(request.SourceText, combined);
            return new AssistantResult(
                combined,
                missing,
                generatedTokenCount,
                processing.Elapsed,
                firstTokenLatency);

            async Task<string> GenerateAsync(string prompt)
            {
                var executor = new StatelessExecutor(weights!, modelParameters!)
                {
                    ApplyTemplate = true,
                    SystemMessage = AssistantPrompts.SystemPrompt
                };
                var inference = new InferenceParams
                {
                    MaxTokens = request.Action == AssistantAction.Summarize ? 1024 : 3072,
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = 0.15f,
                        RepeatPenalty = 1.08f
                    },
                    AntiPrompts = ["</SOURCE_TEXT>", "<|im_end|>"]
                };
                var result = new System.Text.StringBuilder();
                await foreach (var token in executor.InferAsync(prompt, inference, cancellationToken))
                {
                    if (generatedTokenCount == 0)
                    {
                        firstTokenLatency = processing.Elapsed;
                    }
                    generatedTokenCount++;
                    result.Append(token);
                }
                return CleanOutput(result.ToString());
            }
        }
        finally
        {
            operationLock.Release();
        }
    }

    public Task UnloadAsync()
    {
        weights?.Dispose();
        weights = null;
        modelParameters = null;
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await UnloadAsync();
        operationLock.Dispose();
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (weights is not null)
        {
            return;
        }

        var modelPath = Path.Combine(paths.ModelsRoot, "Text", "Qwen3-1.7B-Q4_K_M.gguf");
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Textmodellen saknas.");
        }

        modelParameters = new ModelParams(modelPath)
        {
            ContextSize = 8192,
            GpuLayerCount = 0,
            Threads = Math.Max(1, Environment.ProcessorCount - 1)
        };
        weights = await LLamaWeights.LoadFromFileAsync(modelParameters, cancellationToken);
    }

    private static string CleanOutput(string text)
    {
        var result = text.Trim();
        if (result.StartsWith("```", StringComparison.Ordinal) &&
            result.EndsWith("```", StringComparison.Ordinal))
        {
            var firstLine = result.IndexOf('\n');
            result = result[(firstLine + 1)..^3].Trim();
        }

        var thinkingEnd = result.LastIndexOf("</think>", StringComparison.OrdinalIgnoreCase);
        if (thinkingEnd >= 0)
        {
            result = result[(thinkingEnd + "</think>".Length)..].Trim();
        }

        return result;
    }
}
