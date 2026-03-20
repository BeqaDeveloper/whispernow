using WhisperNow.Native;
using WhisperNow.Services;

namespace WhisperNow;

internal sealed class WhisperNowApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyService _hotkeyService;
    private readonly AudioCaptureService _audioService;
    private readonly TranscriptionService _transcriptionService;
    private bool _isRecording;

    public WhisperNowApp(TranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
        _audioService = new AudioCaptureService();
        _hotkeyService = new HotkeyService();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WhisperNow — Ready (hold LCtrl+LAlt)",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotkeyService.Activated += OnActivated;
        _hotkeyService.Deactivated += OnDeactivated;
        _hotkeyService.Start();

        Log.Info("WhisperNow started — waiting for LCtrl+LAlt");
    }

    private void OnActivated()
    {
        if (_isRecording) return;
        _isRecording = true;
        _trayIcon.Text = "WhisperNow — Recording...";
        _audioService.StartCapture();
        Log.Info("Recording started");
    }

    private async void OnDeactivated()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _audioService.StopCapture();
        Log.Info("Recording stopped");

        // Clear clipboard immediately so a quick Ctrl+V won't paste stale text
        InputInjectionService.ClearClipboard();

        // Remember where the user is RIGHT NOW (before any async gap)
        var targetWindow = NativeMethods.GetForegroundWindow();
        _trayIcon.Text = "WhisperNow — Transcribing...";

        try
        {
            var samples = _audioService.GetCapturedSamples();
            Log.Info($"Samples: {samples.Length} ({samples.Length / 16000.0:F1}s)");

            if (samples.Length < 8000)
            {
                Log.Info("Too short, skipped");
                return;
            }

            var text = await Task.Run(() => _transcriptionService.TranscribeAsync(samples));
            Log.Info($"Result: \"{text}\"");

            if (!string.IsNullOrWhiteSpace(text))
            {
                InputInjectionService.CopyAndPaste(text, targetWindow);
                Log.Info("Pasted");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error: {ex.Message}");
        }
        finally
        {
            _trayIcon.Text = "WhisperNow — Ready (hold LCtrl+LAlt)";
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            ExitThread();
        });
        return menu;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyService.Dispose();
            _audioService.Dispose();
            _transcriptionService.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
