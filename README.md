# ClipPull

*Read this in [French / en français](README.fr.md).*

ClipPull is a portable Windows media downloader built around [yt-dlp](https://github.com/yt-dlp/yt-dlp). Paste one or more links, choose a few simple options, click **Download all**, and save the files locally. No ClipPull account, telemetry, or ClipPull server is required.

## Download

Portable Windows builds are published under [GitHub Releases](https://github.com/Sd-tech-Sol/ClipPull/releases). Download `ClipPull.exe`, place it wherever you want, and run it. No installer is required.

## Platforms

ClipPull passes URLs to yt-dlp and can therefore work with sites supported by that engine, including Facebook/Reels, TikTok, Instagram/Reels, YouTube/Shorts, X/Twitter, Vimeo, and many others. Actual support may change when platforms modify their sites or protections.

## Main features

- Windows 10/11 x64 and one portable `ClipPull.exe`
- English and French interface; English is the first-launch default and can be changed instantly in Settings
- Multiple links at once, processed sequentially
- Visible queue with speed/ETA, removable waiting or finished items, duplicate filtering, and retry of failed items
- Video quality selection: Auto, best, 1080p, 720p, 480p, or smaller file; Auto prefers a ready-to-use stream and falls back to a verified FFmpeg merge only when needed
- Audio-only M4A or MP3
- Optional playlists with a configurable safety limit
- Drag-and-drop or import of `.txt` link lists
- Optional local download history through yt-dlp's `--download-archive`
- Copyable error reports
- Preview with title, platform, duration, and thumbnail when available
- Optional browser-session support through `--cookies-from-browser`
- Persistent preferences and window size/state in `%LOCALAPPDATA%\ClipPull\settings.json`
- Non-blocking ClipPull update notifications through official GitHub Releases, with no automatic executable download or replacement
- About surface with version, license, acknowledgements, and project links

## Automatic dependency updates

At startup, ClipPull checks its components without blocking the interface.

- **yt-dlp** is checked against its official GitHub release. A replacement is installed only after its published SHA-256 is verified.
- **FFmpeg**, when already installed, is checked against the approved version in [`dependencies.json`](dependencies.json). New approved archives must come from `BtbN/FFmpeg-Builds` and pass SHA-256 verification before activation.
- FFmpeg is not downloaded at startup for users who have never needed it. It is installed on demand for audio or video modes that require it.
- Network failures do not prevent ClipPull from starting when a valid local dependency is already available.

## Security and privacy

ClipPull does not reimplement platform extractors. It provides a local interface and launches the official `yt-dlp.exe` binary.

- URLs and arguments are passed with `ProcessStartInfo.ArgumentList` rather than shell-command concatenation.
- yt-dlp and FFmpeg downloads are hash-verified before installation or replacement.
- yt-dlp is launched with `--ignore-config` and `--no-plugin-dirs` so external configuration or plugins are not loaded implicitly.
- Browser cookies are optional and disabled by default; ClipPull does not export them itself.
- ClipPull-specific history and settings remain local.

## Build

Requirements: Windows and the .NET 10 SDK.

```powershell
./scripts/generate-icon.ps1
dotnet publish src/ClipPull/ClipPull.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

GitHub Actions also builds the Windows executable, calculates its SHA-256, and publishes or refreshes the release matching the project version.

## AI-assisted development

ClipPull is an original project by **Sébastien Dubé**. The idea, product direction, requirements, priorities, approvals, and final decisions are his.

Development was AI-assisted using **OpenAI ChatGPT**, **Anthropic Claude Code**, and **OpenAI Codex** for the roles described in the full disclosure. No AI tool is an author, owner, or maintainer, and use of these tools implies no affiliation with or endorsement by OpenAI or Anthropic.

**Final responsibility and maintenance rest with Sébastien Dubé.** See [`AI_ASSISTANCE.md`](AI_ASSISTANCE.md) for the full disclosure.

## Limitations

Platforms regularly change their protections and media formats. A URL may temporarily stop working until yt-dlp is updated. Authenticated content may also depend on the platform and browser. Download only content you are allowed to keep and respect copyright, service terms, and applicable laws.

## License

ClipPull's own code is distributed under the [MIT License](LICENSE). yt-dlp and FFmpeg are independent third-party projects with their own licenses; their binaries are not included in `ClipPull.exe`.

See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md).
