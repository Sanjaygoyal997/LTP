namespace CuringMonitor.Api.Configuration;

/// <summary>
/// Resolves the files the service reads from disk.
///
/// The host takes its content root from the working directory, so double-clicking the
/// executable resolves paths against the shell's directory rather than the one the service
/// was published to, and every relative path misses. The deployed files always sit beside
/// the executable, so that directory is the fallback: a configured relative path is tried
/// against the content root first and against the executable's own directory second.
/// </summary>
public static class ContentPaths
{
    /// <summary>The directory the executable was deployed to.</summary>
    public static string BaseDirectory { get; } =
        AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    /// <summary>
    /// Absolute path for a configured file or directory. Rooted paths are taken as given.
    /// </summary>
    public static string Resolve(string configured, string contentRoot)
    {
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }

        var fromContentRoot = Path.Combine(contentRoot, configured);
        if (File.Exists(fromContentRoot) || Directory.Exists(fromContentRoot))
        {
            return fromContentRoot;
        }

        var beside = Path.Combine(BaseDirectory, configured);
        return File.Exists(beside) || Directory.Exists(beside) ? beside : fromContentRoot;
    }
}
