# Images compressor

A Jellyfin **12** plugin that recompresses the JPEG, PNG and WebP images stored in the Jellyfin
**metadata folder** and in the **library folders** you choose, to save disk space.

It is a plugin version of a shell script based on ImageMagick: the same idea (each file is
processed once and then marked), but it uses the image encoder built into Jellyfin, so no external
tool has to be installed.

## Requirements

- Jellyfin **12.0 or newer** (built against 12.1).
- Linux is the primary target. On Windows the plugin works too, but processed files are tracked in a
  manifest file instead of extended attributes.

## Installation

### From the plugin repository

1. **Dashboard → Plugins → Repositories → +**, then add:

   ```
   https://andreadegiovine.github.io/jellyfin-plugin-images-compressor/repository.json
   ```

2. **Dashboard → Plugins → Catalog**, find **Images compressor** and click **Install**.
3. **Restart Jellyfin.**

### Manual installation

Download the zip from the Releases page, extract it in a new folder inside the Jellyfin `plugins`
directory and restart Jellyfin.

## Settings

Open **Dashboard → Images compressor** (it has its own entry in the dashboard sidebar).

| Setting | What it does |
|---|---|
| **Compression quality** | Quality from 1 to 100 used to re-encode the images (default **80**). |
| **Folders to compress** | One checkbox for the Jellyfin metadata folder and one for every folder of every library. **Nothing is touched until a folder is ticked.** |

## The scheduled task

**Dashboard → Scheduled Tasks → Images compressor → Compress images**

- It is **queued automatically after a library scan**: when libraries stop changing for two minutes,
  or when the standard *Scan Media Library* task ends, the task is added to the queue.
- It can also be started manually, or given its own triggers from the same page.
- Only the ticked folders that still exist in Jellyfin are processed.

## Run history

The settings page ends with a **Run history** table with the details of the latest 50 runs (newest
first): start and end time, total time, status, quality, analyzed paths, files to process, files
processed, errors (expandable list), initial size, final size and space saved. Runs that find no
new images are not recorded. The data is kept in `images-compressor/runs.json` in the Jellyfin data folder.

## How it works

- Files with extension `jpg`, `jpeg`, `png` or `webp` are searched recursively (symbolic links are not followed).
- Every processed file is marked with the `user.compressed` extended attribute, **the same one used by
  the original script**: files already compressed by the script are skipped. When the file system
  does not support extended attributes, a manifest in the Jellyfin data folder
  (`images-compressor/manifest.json`) is used, validated against file size and modification time.
- A file is re-encoded and replaced **only if the result is smaller**; in that case it is marked
  anyway, so it is not retried at every run.
- The file is overwritten in place: owner, permissions and modification time are preserved, so
  Jellyfin does not see the images as changed.
- A new image downloaded by Jellyfin replaces the old file and loses the mark, so it is compressed
  on the next run.

## Things worth knowing

- Compression is **lossy and irreversible** for JPEG and WebP. Images stored next to the media
  files (posters, backdrops) are replaced too when the library folder is ticked.
- PNG is lossless: the quality value has little effect and the gain is usually small.
- Metadata (EXIF) is not kept: the image is re-encoded by Jellyfin's encoder.
- Files are processed one at a time, so the load on the server stays low.

## Troubleshooting

Every log line starts with `Images compressor:`; filter the Jellyfin log (**Dashboard → Logs**) on
that text. At the end of every run a summary line reports processed files, errors and space saved.

| Symptom | Likely cause |
|---|---|
| `the Jellyfin image encoder is not available` | The native Skia library is missing: the server cannot encode images. |
| `no directory enabled, nothing to do` | No folder is ticked in the settings page. |
| Same files processed at every run | The result is not smaller and the file system rejects extended attributes and the manifest cannot be written. Check permissions on the Jellyfin data folder. |

## Building from source

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build -c Release
```

The output is `Jellyfin.Plugin.ImagesCompressor/bin/Release/net10.0/Jellyfin.Plugin.ImagesCompressor.dll`.

## Releasing

Publishing a GitHub Release (tag `vX.Y.Z.W`) runs `.github/workflows/publish.yaml`, which builds the
plugin, uploads the zip with its checksums to the release and regenerates the plugin repository
manifest (`repository.json`) on the `gh-pages` branch.

## License

Apache License 2.0, see [LICENSE](LICENSE).
