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
            Log.Info("Downloading ggml-small.en model (~460 MB) — this is one-time...");
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
            .WithThreads(Math.Max(1, Environment.ProcessorCount - 2))
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
    }

    public async Task<string> TranscribeAsync(float[] samples)
    {
        if (_processor == null)
            throw new InvalidOperationException("TranscriptionService not initialized.");

        using var wavStream = BuildWavStream(samples);
        var segments = new StringBuilder();

        await foreach (var segment in _processor.ProcessAsync(wavStream))
        {
            if (segments.Length > 0)
                segments.Append(' ');
            segments.Append(segment.Text.Trim());
        }

        return segments.ToString().Trim();
    }

    private static MemoryStream BuildWavStream(float[] samples, int sampleRate = 16000)
    {
        const int bitsPerSample = 16;
        int dataLength = samples.Length * sizeof(short);

        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + dataLength);
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));

        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * bitsPerSample / 8);
        writer.Write((short)(bitsPerSample / 8));
        writer.Write((short)bitsPerSample);

        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(dataLength);
        foreach (var sample in samples)
            writer.Write((short)(Math.Clamp(sample, -1f, 1f) * short.MaxValue));

        stream.Position = 0;
        return stream;
    }

    public void Dispose()
    {
        _processor?.Dispose();
        _factory?.Dispose();
    }
}
