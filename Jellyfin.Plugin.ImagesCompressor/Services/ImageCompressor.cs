using System;
using System.IO;
using MediaBrowser.Controller.Drawing;
using MediaBrowser.Model.Drawing;

namespace Jellyfin.Plugin.ImagesCompressor.Services;

/// <summary>
/// Re-encodes a single image with the image encoder built into Jellyfin.
/// </summary>
public sealed class ImageCompressor
{
    private readonly IImageEncoder _encoder;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImageCompressor"/> class.
    /// </summary>
    /// <param name="encoder">Instance of the <see cref="IImageEncoder"/> interface.</param>
    public ImageCompressor(IImageEncoder encoder)
    {
        _encoder = encoder;
    }

    /// <summary>
    /// Gets a value indicating whether the server has a working image encoder.
    /// </summary>
    public bool IsAvailable => _encoder.SupportsImageEncoding;

    /// <summary>
    /// Checks whether the file has an extension handled by the plugin (jpg, jpeg, png, webp).
    /// </summary>
    /// <param name="path">File path.</param>
    /// <returns><c>true</c> if the file is a supported image.</returns>
    public static bool IsSupportedImage(string path)
    {
        return TryGetFormat(path, out _);
    }

    /// <summary>
    /// Re-encodes the image in place (same inode, owner, permissions and modification time) with the
    /// given quality. The file is only replaced when the result is smaller than the original.
    /// </summary>
    /// <param name="file">The image file.</param>
    /// <param name="quality">Quality, 1-100.</param>
    /// <returns>The number of bytes saved (0 when the file was left unchanged).</returns>
    public long Compress(string file, int quality)
    {
        if (!TryGetFormat(file, out var format))
        {
            throw new InvalidOperationException("Unsupported image format");
        }

        var info = new FileInfo(file);
        var originalLength = info.Length;
        var originalWriteTime = info.LastWriteTimeUtc;
        var tempFile = Path.Combine(Path.GetTempPath(), "imagescompressor_" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            var options = new ImageProcessingOptions
            {
                Quality = quality,
                SupportedOutputFormats = _encoder.SupportedOutputFormats
            };

            // autoOrient = true makes the encoder always re-encode, even with default options.
            var result = _encoder.EncodeImage(file, originalWriteTime, tempFile, true, null, quality, options, format);
            if (!string.Equals(result, tempFile, StringComparison.Ordinal) || !File.Exists(tempFile))
            {
                throw new InvalidOperationException("The image encoder did not produce an output file");
            }

            var newLength = new FileInfo(tempFile).Length;
            if (newLength <= 0 || newLength >= originalLength)
            {
                return 0;
            }

            // Overwrite in place so owner, permissions and hard links are preserved.
            File.WriteAllBytes(file, File.ReadAllBytes(tempFile));
            File.SetLastWriteTimeUtc(file, originalWriteTime);
            return originalLength - newLength;
        }
        finally
        {
            try
            {
                File.Delete(tempFile);
            }
            catch (IOException)
            {
                // best effort
            }
        }
    }

    private static bool TryGetFormat(string path, out ImageFormat format)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".jpg":
            case ".jpeg":
                format = ImageFormat.Jpg;
                return true;
            case ".png":
                format = ImageFormat.Png;
                return true;
            case ".webp":
                format = ImageFormat.Webp;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
