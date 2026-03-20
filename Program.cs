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

        Log.Info("=== WhisperNow starting ===");

        var modelsDir = Path.Combine(AppContext.BaseDirectory, "models");
        Log.Info($"Models directory: {modelsDir}");

        var transcription = new TranscriptionService();
        try
        {
            await transcription.InitializeAsync(modelsDir);
            Log.Info("Whisper model loaded OK");
        }
        catch (Exception ex)
        {
            Log.Error($"Model load failed: {ex}");
            MessageBox.Show(
                $"Failed to load Whisper model:\n\n{ex.Message}\n\nPlace ggml-tiny.en.bin in the models/ folder.",
                "WhisperNow", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var app = new WhisperNowApp(transcription);
        Application.Run(app);
    }
}
