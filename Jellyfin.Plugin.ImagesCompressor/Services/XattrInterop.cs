using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Minimal access to the extended attributes of Linux file systems. The plugin uses the same
/// <c>user.compressed</c> attribute as the original shell script, so files already processed by
/// the script are recognized and skipped.
/// </summary>
internal static class XattrInterop
{
    private static readonly byte[] TrueValue = Encoding.ASCII.GetBytes("true");

    /// <summary>
    /// Gets a value indicating whether extended attributes can be used on this platform.
    /// </summary>
    public static bool IsSupported => OperatingSystem.IsLinux();

    /// <summary>
    /// Checks whether the attribute exists on the file.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="name">Attribute name (for example <c>user.compressed</c>).</param>
    /// <returns><c>true</c> if the attribute is set.</returns>
    public static bool Has(string path, string name)
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            return GetXattr(path, name, null, 0) >= 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sets the attribute to <c>true</c>.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="name">Attribute name.</param>
    /// <returns><c>true</c> if the attribute was set; <c>false</c> if the file system does not support it.</returns>
    public static bool SetTrue(string path, string name)
    {
        if (!IsSupported)
        {
            return false;
        }

        try
        {
            return SetXattr(path, name, TrueValue, (nuint)TrueValue.Length, 0) == 0;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }

    [DllImport("libc", EntryPoint = "getxattr", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern nint GetXattr(string path, string name, byte[]? value, nuint size);

    [DllImport("libc", EntryPoint = "setxattr", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int SetXattr(string path, string name, byte[] value, nuint size, int flags);
}
