namespace WhisperNow;

internal static class Log
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "whispernow.log");
    private static readonly object Lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        lock (Lock)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
    }
}
