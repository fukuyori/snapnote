using System.Diagnostics;
using System.Text;

namespace SnapNoteStudio.Services;

/// <summary>
/// Lightweight one-line diagnostic log for capture timing and tray tooltip events.
/// </summary>
public static class DiagnosticLog
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly object LogSync = new();

    public static string LogPath { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SnapNoteStudio",
        "logs",
        "diagnostics.log");

    public static void Write(string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}";

            lock (LogSync)
            {
                var directory = System.IO.Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                    System.IO.Directory.CreateDirectory(directory);

                var file = new System.IO.FileInfo(LogPath);
                if (file.Exists && file.Length > MaxLogBytes)
                    System.IO.File.Move(LogPath, System.IO.Path.ChangeExtension(LogPath, ".old.log"), true);

                System.IO.File.AppendAllText(LogPath, line, Encoding.UTF8);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Diagnostic log failed: {ex.Message}");
        }
    }
}
