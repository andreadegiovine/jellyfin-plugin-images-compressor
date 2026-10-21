using System;
using System.IO;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Helpers to compare and resolve directory paths.
/// </summary>
internal static class PathUtils
{
    /// <summary>
    /// Gets the comparer to use for file system paths on the current platform.
    /// </summary>
    public static StringComparer Comparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Normalizes a path: absolute, without trailing directory separator.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The normalized path.</returns>
    public static string Normalize(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>
    /// Resolves symbolic links of a directory (the equivalent of <c>realpath</c>).
    /// </summary>
    /// <param name="path">The directory path.</param>
    /// <returns>The real path of the directory.</returns>
    public static string Resolve(string path)
    {
        try
        {
            var target = new DirectoryInfo(path).ResolveLinkTarget(true);
            return target is null ? Normalize(path) : Normalize(target.FullName);
        }
        catch (IOException)
        {
            return Normalize(path);
        }
    }
}
