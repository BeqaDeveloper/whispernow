using System.Runtime;
using System.Text;
using Whisper.net;
using Whisper.net.Ggml;

namespace WhisperNow.Services;

internal sealed class TranscriptionService : IDisposable
{
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;

    public async Task InitializeAsync(string modelsDirectory)
    {
        var modelPath = Path.Combine(modelsDirectory, "ggml-small.en.bin");

        if (!File.Exists(modelPath))
        {
            Log.Info("Downloading ggml-small.en model (~460 MB) — one-time...");
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(GgmlType.SmallEn);
            Directory.CreateDirectory(modelsDirectory);
            using var fileWriter = File.Create(modelPath);
            await modelStream.CopyToAsync(fileWriter);
            Log.Info("Model downloaded.");
        }

        _factory = WhisperFactory.FromPath(modelPath);
        _processor = _factory.CreateBuilder()
            .WithLanguage("en")
            .WithThreads(8)
            .WithPrompt(
                "SOLID principles, single responsibility, open-closed, Liskov substitution, " +
                "interface segregation, dependency inversion, " +
                "SQL, SELECT, WHERE, FROM, JOIN, LEFT JOIN, INNER JOIN, GROUP BY, ORDER BY, " +
                "INSERT, UPDATE, DELETE, HAVING, DISTINCT, UNION, subquery, stored procedure, " +
                ".NET, .NET Core, .NET Framework, ASP.NET, Entity Framework, LINQ, NuGet, " +
                "C#, async, await, interface, abstract class, dependency injection, middleware, " +
                "API, REST, GraphQL, HTTP, HTTPS, JSON, XML, YAML, " +
                "Azure, AWS, Docker, Kubernetes, CI/CD, GitHub, Git, " +
                "JavaScript, TypeScript, Python, React, Angular, Node.js, " +
                "microservice, repository pattern, unit test, integration test")
            .Build();

        await WarmUpAsync();
    }

    /// <summary>
    /// Runs a tiny dummy transcription so Whisper's internal buffers,
    /// thread pool, and CPU caches are all hot for the first real call.
    /// </summary>
    private async Task WarmUpAsync()
    {
        var silence = new float[16000]; // 1 second of silence
        await foreach (var _ in _processor!.ProcessAsync(silence)) { }
        Log.Info("Whisper warm-up complete");
    }

    public async Task<string> TranscribeAsync(float[] samples)
    {
        if (_processor == null)
            throw new InvalidOperationException("TranscriptionService not initialized.");

        // Suppress GC during inference to avoid random pauses
        bool noGc = false;
        try
        {
            if (GC.TryStartNoGCRegion(50 * 1024 * 1024)) // 50 MB headroom
                noGc = true;
        }
        catch { /* ignore if already in no-gc region */ }

        try
        {
            var segments = new StringBuilder();

            await foreach (var segment in _processor.ProcessAsync(samples))
            {
                if (segments.Length > 0)
                    segments.Append(' ');
                segments.Append(segment.Text.Trim());
            }

            return segments.ToString().Trim();
        }
        finally
        {
            if (noGc)
            {
                try { GCSettings.LatencyMode = GCLatencyMode.Interactive; }
                catch { /* already ended */ }
            }
        }
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
    }
}
