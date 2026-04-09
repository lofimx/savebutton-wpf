namespace Kaya.Core.Services;

public class Logger
{
    private static readonly string KayaDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kaya");
    private static readonly string LogPath = Path.Combine(KayaDir, "desktop-app-log");

    private static readonly Lazy<Logger> _instance = new(() => new Logger());
    public static Logger Instance => _instance.Value;

    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    private Logger()
    {
        Directory.CreateDirectory(KayaDir);
        _writer = new StreamWriter(LogPath, append: true) { AutoFlush = true };
    }

    public void Log(string message)
    {
        WriteToFile(message);
        Console.WriteLine(message);
    }

    public void Error(string message)
    {
        WriteToFile(message);
        Console.Error.WriteLine(message);
    }

    private void WriteToFile(string message)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("o");
        var line = $"{timestamp} {message}";
        lock (_lock)
        {
            _writer.WriteLine(line);
        }
    }
}
