using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace WhisperNow.Services;

internal sealed class AudioCaptureService : IDisposable
{
    private const int TargetSampleRate = 16000;

    private WasapiLoopbackCapture? _capture;
    private WaveFormat? _captureFormat;
    private MemoryStream? _buffer;
    private readonly object _lock = new();
    private bool _initialized;

    public void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized) return;
            _capture = new WasapiLoopbackCapture();
            _captureFormat = _capture.WaveFormat;
            _capture.DataAvailable += OnDataAvailable;
            _capture.RecordingStopped += OnRecordingStopped;
            _initialized = true;
            Log.Info($"WASAPI pre-initialized: {_captureFormat.SampleRate}Hz, {_captureFormat.Channels}ch");
        }
    }

    public void StartCapture()
    {
        lock (_lock)
        {
            EnsureInitialized();
            _buffer?.Dispose();
            _buffer = new MemoryStream();

            try
            {
                _capture!.StartRecording();
            }
            catch (InvalidOperationException)
            {
                // Device may have changed, reinitialize
                ReinitDevice();
                _capture!.StartRecording();
            }
        }
    }

    public void StopCapture()
    {
        lock (_lock)
        {
            if (_capture is { CaptureState: CaptureState.Capturing })
                _capture.StopRecording();
        }
    }

    public float[] GetCapturedSamples()
    {
        lock (_lock)
        {
            if (_buffer == null || _captureFormat == null || _buffer.Length == 0)
                return [];

            var rawData = _buffer.ToArray();
            _buffer.Dispose();
            _buffer = null;

            return ConvertTo16kMono(rawData, _captureFormat);
        }
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        lock (_lock)
        {
            _buffer?.Write(e.Buffer, 0, e.BytesRecorded);
        }
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception != null)
            Log.Error($"Recording error: {e.Exception.Message}");
    }

    private void ReinitDevice()
    {
        _capture?.Dispose();
        _capture = new WasapiLoopbackCapture();
        _captureFormat = _capture.WaveFormat;
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;
        Log.Info("WASAPI re-initialized (device changed)");
    }

    private static float[] ConvertTo16kMono(byte[] rawData, WaveFormat sourceFormat)
    {
        using var rawStream = new RawSourceWaveStream(new MemoryStream(rawData), sourceFormat);
        ISampleProvider pipeline = rawStream.ToSampleProvider();

        if (pipeline.WaveFormat.Channels > 1)
            pipeline = new MonoDownmixSampleProvider(pipeline);

        if (pipeline.WaveFormat.SampleRate != TargetSampleRate)
            pipeline = new WdlResamplingSampleProvider(pipeline, TargetSampleRate);

        // Pre-allocate based on expected output size
        int totalSourceSamples = rawData.Length / (sourceFormat.BitsPerSample / 8);
        int monoSamples = totalSourceSamples / sourceFormat.Channels;
        int estimatedOutput = (int)((long)monoSamples * TargetSampleRate / sourceFormat.SampleRate) + 1024;

        var result = new float[estimatedOutput];
        int offset = 0;
        int samplesRead;
        while ((samplesRead = pipeline.Read(result, offset, Math.Min(8192, result.Length - offset))) > 0)
        {
            offset += samplesRead;
            if (offset >= result.Length)
                break;
        }

        if (offset < result.Length)
            Array.Resize(ref result, offset);
        return result;
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
