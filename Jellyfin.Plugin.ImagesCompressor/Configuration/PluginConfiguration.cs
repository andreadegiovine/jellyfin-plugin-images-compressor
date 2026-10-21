using System;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ImagesCompressor.Configuration;

/// <summary>
/// Persistent plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Default value of <see cref="Quality"/>.
    /// </summary>
    public const int DefaultQuality = 80;

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Quality = DefaultQuality;
        EnabledPaths = Array.Empty<string>();
    }

    /// <summary>
    /// Gets or sets the compression quality (1-100) used to re-encode the images.
    /// </summary>
    public int Quality { get; set; }

    /// <summary>
    /// Gets or sets the directories (metadata folder and library folders) whose images are
    /// compressed. Nothing is touched until a directory is ticked in the configuration page.
    /// </summary>
#pragma warning disable CA1819 // XML serialization of the configuration needs an array
    public string[] EnabledPaths { get; set; }
#pragma warning restore CA1819
}
