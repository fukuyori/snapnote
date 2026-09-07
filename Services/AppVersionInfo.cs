namespace SnapNoteStudio.Services;

public static class AppVersionInfo
{
    public static string Current { get; } = GetCurrentVersion();

    private static string GetCurrentVersion()
    {
        var version = typeof(AppVersionInfo).Assembly.GetName().Version;
        return version == null ? string.Empty : version.ToString(3);
    }
}
