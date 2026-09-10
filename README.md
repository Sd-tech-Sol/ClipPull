# ClipPull

*Read this in [French / en français](README.fr.md).*

ClipPull is a small portable Windows media downloader built around [yt-dlp](https://github.com/yt-dlp/yt-dlp).

The goal is simple: paste one or more links, choose a few straightforward options, click **Download all**, and save the files locally. No ClipPull account, no telemetry, and no ClipPull server.

## Download

The portable Windows build is published under **Releases**:

https://github.com/Sd-tech-Sol/ClipPull/releases

Download `ClipPull.exe`, place it wherever you want, and run it. No installer is required.

## Platforms

ClipPull passes URLs to the yt-dlp engine. It can therefore work with sites supported by yt-dlp, including Facebook/Reels, TikTok, Instagram/Reels, YouTube/Shorts, X/Twitter, Vimeo, and many others.

Actual support depends on yt-dlp and may change when a platform modifies its site or protections.

## New in v0.3.1: automatic dependency updates

At startup, ClipPull opens its interface immediately and then checks its components automatically:

- **yt-dlp**: ClipPull checks the official release, compares the published SHA-256, and replaces the local engine only when the downloaded binary matches the official checksum.
- **FFmpeg**: if FFmpeg is already installed, ClipPull reads `dependencies.json`, the repository's approved-version manifest. If a newer approved version is available, the archive is downloaded and verified by SHA-256 before it becomes active. The previous version is removed only after a successful installation.
- If FFmpeg has never been used, ClipPull does not download it unnecessarily at startup. It is installed automatically the first time an audio or high-quality video option requires it.
- A network outage or update failure does not prevent ClipPull from starting; the last valid local copy remains usable.
- An archive whose SHA-256 does not match is never installed.

The FFmpeg manifest accepts only HTTPS URLs from the official `BtbN/FFmpeg-Builds` repository, Windows x64 LGPL builds, and valid SHA-256 hashes.

## The 7 additions introduced in v0.3.0

1. **Video quality selection** — Auto (fast), best quality, 1080p max, 720p max, 480p max, or smaller file.
2. **Audio only** — extract as M4A or MP3. FFmpeg is installed locally only when this feature is used.
3. **Full playlists** — explicit option, disabled by default, with confirmation and a configurable per-link limit (50 items by default) to reduce accidental mass downloads.
4. **Drag and drop a `.txt` file** — drop a text file containing links directly onto the window; the normal import button remains available.
5. **Optional local history** — enabled by default through yt-dlp's `--download-archive` mechanism to avoid downloading the same content again. The history can be cleared from the interface without deleting downloaded media.
6. **Copyable error report** — failures are kept for the current session and can be copied in one click with platform, URL, and a useful error message. Failed items can also be retried.
7. **Preview before downloading** — the first link can be analyzed to show its title, platform, duration when available, and thumbnail.

## Existing features

- Windows 10/11 x64
- One portable `ClipPull.exe`
- Multiple links at once, one per line
- Sequential downloads
- Exact duplicate detection and removal in the input list
- Visible queue with platform and status for each link
- Progress for the current download and `x/y` queue progress
- One failed link does not stop the remaining queue
- **Retry failed items** button
- Import link lists from `.txt`
- Clipboard URL detection at startup
- Default destination: `Downloads\ClipPull`
- Optional local browser-session support through `--cookies-from-browser`
- Chrome, Edge, Firefox, Brave, Chromium, Opera, and Vivaldi options
- yt-dlp downloaded from its official release and verified with SHA-256

## FFmpeg: on demand and from an approved version

Audio modes and video qualities that may require merging separate streams need FFmpeg. ClipPull does not bundle FFmpeg inside its executable.

The first time a feature requires it, ClipPull asks for confirmation and retrieves the approved version defined in [`dependencies.json`](dependencies.json). If the manifest cannot be reached during a first installation, ClipPull keeps a pinned fallback version in the application code. The archive is verified with SHA-256 before extraction. Only `ffmpeg.exe` and `ffprobe.exe` are extracted under `%LOCALAPPDATA%\ClipPull\ffmpeg`.

The initial manifest approves: `n9.0.1-26-g5c8e7e2433`

Initial archive SHA-256: `4700c0bcb523466fdf5e36e22ad4ff3fadf33f203e2dbfdc78f5b4cd068b8818`

After a successful update, `active-version.txt` records the active local FFmpeg version. An interrupted or invalid installation never replaces the previously active version.

## Local history

When local history is enabled, ClipPull uses yt-dlp's download archive directly:

`%LOCALAPPDATA%\ClipPull\download-archive.txt`

This file stores the identifiers needed to recognize content that has already been downloaded. It can be deleted from the interface with **Clear history**. No history is sent to ClipPull or to a ClipPull server.

## Operation and security

ClipPull does not reimplement platform extractors. It provides a local interface and launches the official `yt-dlp.exe` binary.

- URLs and other parameters are passed with `ProcessStartInfo.ArgumentList`, not by concatenating a shell command.
- yt-dlp is downloaded only from its official GitHub release and its SHA-256 is verified before installation or replacement.
- ClipPull launches yt-dlp with `--ignore-config` and `--no-plugin-dirs` so external yt-dlp configuration or plugin directories are not loaded implicitly.
- Browser cookies are optional and disabled by default; ClipPull does not export them itself.
- FFmpeg is controlled by an approved-version manifest restricted to `BtbN/FFmpeg-Builds`, and every archive is verified with SHA-256 before extraction.
- ClipPull-specific data remains local. Requests required for downloads, previews, and update checks are sent to the relevant platforms, GitHub, and the official component sources.

## Build

Requirements: Windows and the .NET 10 SDK.

```powershell
./scripts/generate-icon.ps1
dotnet publish src/ClipPull/ClipPull.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

The main output is `dist\ClipPull.exe`. GitHub Actions also builds the Windows executable automatically, calculates its SHA-256, and publishes or refreshes the release matching the project version so it points to the latest commit for that version.

## Limitations

Platforms regularly change their protections and media formats. A URL may temporarily stop working until yt-dlp is updated. Previewing or downloading authenticated content may also fail depending on the platform or browser.

Playlist mode is intentionally limited by default. Increasing the limit can download a large amount of data.

Download only content you are allowed to keep, and respect copyright, service terms, and applicable laws.

## License

ClipPull's own code is distributed under the MIT License. yt-dlp and FFmpeg are independent third-party projects with their own licenses; their binaries are not included in `ClipPull.exe`.

See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
