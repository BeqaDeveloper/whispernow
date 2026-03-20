using WhisperNow.Native;
using WhisperNow.Services;

namespace WhisperNow;

internal sealed class WhisperNowApp : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HotkeyService _hotkeyService;
    private readonly AudioCaptureService _audioService;
    private readonly TranscriptionService _transcriptionService;
    private readonly OverlayForm _overlay;
    private readonly Icon _idleIcon;
    private readonly Icon _recordingIcon;
    private readonly Icon _processingIcon;
    private readonly Icon _disabledIcon;
    private bool _isRecording;
    private bool _serviceEnabled = true;

    public WhisperNowApp(TranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
        _audioService = new AudioCaptureService();
        _hotkeyService = new HotkeyService();

        _idleIcon = CreateCircleIcon(Color.FromArgb(60, 180, 75));
        _recordingIcon = CreateCircleIcon(Color.FromArgb(220, 40, 40));
        _processingIcon = CreateCircleIcon(Color.FromArgb(255, 165, 0));
        _disabledIcon = CreateCircleIcon(Color.FromArgb(120, 120, 120));

        _overlay = new OverlayForm();
        _overlay.Show();

        _trayIcon = new NotifyIcon
        {
            Icon = _idleIcon,
            Text = "WhisperNow — Ready",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _hotkeyService.Activated += OnActivated;
        _hotkeyService.Deactivated += OnDeactivated;
        _hotkeyService.Start();

        // Pre-initialize audio device so first recording is instant
        _audioService.EnsureInitialized();

        Log.Info("WhisperNow started");
    }

    private void OnActivated()
    {
        if (!_serviceEnabled || _isRecording) return;
        _isRecording = true;
        _trayIcon.Icon = _recordingIcon;
        _trayIcon.Text = "WhisperNow — Recording...";
        _overlay.SetState(OverlayState.Recording);
        _audioService.StartCapture();
        Log.Info("Recording started");
    }

    private async void OnDeactivated()
    {
        if (!_isRecording) return;
        _isRecording = false;
        _audioService.StopCapture();
        Log.Info("Recording stopped");

        InputInjectionService.ClearClipboard();

        var targetWindow = NativeMethods.GetForegroundWindow();
        _trayIcon.Icon = _processingIcon;
        _trayIcon.Text = "WhisperNow — Transcribing...";
        _overlay.SetState(OverlayState.Transcribing);

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
            _trayIcon.Icon = _serviceEnabled ? _idleIcon : _disabledIcon;
            _trayIcon.Text = _serviceEnabled ? "WhisperNow — Ready" : "WhisperNow — Paused";
            _overlay.SetState(_serviceEnabled ? OverlayState.Ready : OverlayState.Disabled);
        }
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var toggleService = new ToolStripMenuItem("Pause service");
        toggleService.Click += (_, _) =>
        {
            _serviceEnabled = !_serviceEnabled;
            toggleService.Text = _serviceEnabled ? "Pause service" : "Resume service";
            _trayIcon.Icon = _serviceEnabled ? _idleIcon : _disabledIcon;
            _trayIcon.Text = _serviceEnabled ? "WhisperNow — Ready" : "WhisperNow — Paused";
            _overlay.SetState(_serviceEnabled ? OverlayState.Ready : OverlayState.Disabled);
            Log.Info($"Service {(_serviceEnabled ? "resumed" : "paused")}");
        };
        menu.Items.Add(toggleService);

        var toggleOverlay = new ToolStripMenuItem("Hide overlay");
        toggleOverlay.Click += (_, _) =>
        {
            if (_overlay.Visible)
            {
                _overlay.Hide();
                toggleOverlay.Text = "Show overlay";
            }
            else
            {
                _overlay.Show();
                toggleOverlay.Text = "Hide overlay";
            }
        };
        menu.Items.Add(toggleOverlay);

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _trayIcon.Visible = false;
            _overlay.Close();
            ExitThread();
        });

        return menu;
    }

    private static Icon CreateCircleIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hotkeyService.Dispose();
            _audioService.Dispose();
            _transcriptionService.Dispose();
            _trayIcon.Dispose();
            _overlay.Dispose();
            _idleIcon.Dispose();
            _recordingIcon.Dispose();
            _processingIcon.Dispose();
            _disabledIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
