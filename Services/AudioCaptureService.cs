using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WhisperNow.Services;

internal sealed class AudioCaptureService : IDisposable
{
    private const int TargetSampleRate = 16000;

    private WasapiLoopbackCapture? _capture;
    private MemoryStream? _buffer;
    private WaveFormat? _captureFormat;
    private readonly object _lock = new();
    private int _dataChunks;

    public void StartCapture()
    {
        lock (_lock)
        {
            _buffer?.Dispose();
            _buffer = new MemoryStream();
            _dataChunks = 0;

            _capture?.Dispose();
            _capture = new WasapiLoopbackCapture();
            _captureFormat = _capture.WaveFormat;
            Log.Info($"Audio capture format: {_captureFormat.SampleRate}Hz, {_captureFormat.Channels}ch, {_captureFormat.BitsPerSample}bit");
            _capture.DataAvailable += OnDataAvailable;
            _capture.StartRecording();
            Log.Info("Audio capture STARTED");
        }
    }

    public void StopCapture()
    {
        lock (_lock)
        {
            if (_capture is { CaptureState: NAudio.CoreAudioApi.CaptureState.Capturing })
            {
                _capture.StopRecording();
                Log.Info($"Audio capture STOPPED — received {_dataChunks} data chunks, buffer={_buffer?.Length ?? 0} bytes");
            }
            else
            {
                Log.Info($"StopCapture called but state={_capture?.CaptureState}");
            }
        }
    }

    public float[] GetCapturedSamples()
    {
        lock (_lock)
        {
            if (_buffer == null || _captureFormat == null || _buffer.Length == 0)
            {
                Log.Info("GetCapturedSamples: no data (buffer empty or null)");
                return [];
            }

            var rawData = _buffer.ToArray();
            _buffer.Dispose();
            _buffer = null;

            Log.Info($"Converting {rawData.Length} bytes from {_captureFormat.SampleRate}Hz to 16kHz mono...");
            var samples = ConvertTo16kMono(rawData, _captureFormat);
            Log.Info($"Conversion done: {samples.Length} float samples ({samples.Length / 16000.0:F1}s at 16kHz)");
            return samples;
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _dataChunks++;
            _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private static float[] ConvertTo16kMono(byte[] rawData, WaveFormat sourceFormat)
    {
        using var rawStream = new RawSourceWaveStream(new MemoryStream(rawData), sourceFormat);
        ISampleProvider pipeline = rawStream.ToSampleProvider();

        if (pipeline.WaveFormat.Channels > 1)
            pipeline = new MonoDownmixSampleProvider(pipeline);

        if (pipeline.WaveFormat.SampleRate != TargetSampleRate)
            pipeline = new WdlResamplingSampleProvider(pipeline, TargetSampleRate);

        var result = new List<float>();
        var readBuffer = new float[4096];
        int samplesRead;
        while ((samplesRead = pipeline.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            for (int i = 0; i < samplesRead; i++)
                result.Add(readBuffer[i]);
        }

        return result.ToArray();
    }

    public void Dispose()
    {
        _capture?.Dispose();
        _buffer?.Dispose();
    }

    private sealed class MonoDownmixSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        private readonly int _channels;
        private float[]? _sourceBuffer;

        public WaveFormat WaveFormat { get; }

        public MonoDownmixSampleProvider(ISampleProvider source)
        {
            _source = source;
            _channels = source.WaveFormat.Channels;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 1);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int sourceCount = count * _channels;
            if (_sourceBuffer == null || _sourceBuffer.Length < sourceCount)
                _sourceBuffer = new float[sourceCount];

            int sourceSamplesRead = _source.Read(_sourceBuffer, 0, sourceCount);
            int monoSamples = sourceSamplesRead / _channels;

            for (int i = 0; i < monoSamples; i++)
            {
                float sum = 0;
                for (int ch = 0; ch < _channels; ch++)
                    sum += _sourceBuffer[i * _channels + ch];
                buffer[offset + i] = sum / _channels;
            }

            return monoSamples;
        }
    }
}
