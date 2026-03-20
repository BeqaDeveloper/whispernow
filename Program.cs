using System.Diagnostics;
using WhisperNow;
using WhisperNow.Services;

static class Program
{
    [STAThread]
    static async Task Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Elevate process priority so Whisper gets CPU time promptly
        try { Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High; }
        catch { /* requires admin on some systems */ }

        Log.Info("=== WhisperNow starting ===");

        var modelsDir = Path.Combine(AppContext.BaseDirectory, "models");
        Log.Info($"Models directory: {modelsDir}");

        var transcription = new TranscriptionService();
        try
        {
            await transcription.InitializeAsync(modelsDir);
        }
        catch (Exception ex)
        {
            Log.Error($"Model load failed: {ex}");
            MessageBox.Show(
                $"Failed to load Whisper model:\n\n{ex.Message}",
                "WhisperNow", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var app = new WhisperNowApp(transcription);
        Application.Run(app);
    }
}
