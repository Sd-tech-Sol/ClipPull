using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using ClipPull.Localization;

namespace ClipPull.Services;

internal sealed class FfmpegManager
{
    private const string ManifestUrl = "https://raw.githubusercontent.com/Sd-tech-Sol/ClipPull/main/dependencies.json";

    // Safe offline fallback for first install if the manifest cannot be reached.
    private const string BootstrapVersion = "n9.0.1-26-g5c8e7e2433";
    private const string BootstrapArchiveUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-06-13-06/ffmpeg-n9.0.1-26-g5c8e7e2433-win64-lgpl-9.0.zip";
    private const string BootstrapSha256 = "4700c0bcb523466fdf5e36e22ad4ff3fadf33f203e2dbfdc78f5b4cd068b8818";

    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _baseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "ffmpeg");

    private string ActiveVersionPath => Path.Combine(_baseDirectory, "active-version.txt");

    public bool IsInstalled => TryGetInstalledDirectory(out _);

    public async Task<string> EnsureAsync(Action<LocalizedMessage>? status, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_baseDirectory);

            if (TryGetInstalledDirectory(out var installedDirectory))
            {
                status?.Invoke(new("Service.FfmpegLocalReady"));
                return installedDirectory;
            }

            ApprovedRelease release;
            try
            {
                status?.Invoke(new("Service.FfmpegCheckingApproved"));
                release = await GetApprovedReleaseAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                release = GetBootstrapRelease();
                status?.Invoke(new("Service.FfmpegManifestFallback"));
            }

            return await InstallReleaseAsync(release, status, progress, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> UpdateInstalledAsync(Action<LocalizedMessage>? status, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (!TryGetInstalledDirectory(out _))
        {
            status?.Invoke(new("Service.FfmpegNotInstalledNoUpdate"));
            return LocalizationService.Get("Service.FfmpegNotInstalled");
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!TryGetInstalledDirectory(out var currentDirectory))
                return LocalizationService.Get("Service.FfmpegNotInstalled");

            status?.Invoke(new("Service.FfmpegCheckingManifest"));
            var release = await GetApprovedReleaseAsync(cancellationToken);
            var currentVersion = Path.GetFileName(currentDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.Equals(currentVersion, release.Version, StringComparison.OrdinalIgnoreCase))
            {
                await WriteActiveVersionAsync(release.Version, cancellationToken);
                status?.Invoke(new("Service.FfmpegUpToDate"));
                return LocalizationService.Get("Service.FfmpegUpToDate");
            }

            status?.Invoke(new("Service.FfmpegUpdating", currentVersion, release.Version));
            var installed = await InstallReleaseAsync(release, status, progress, cancellationToken);
            CleanupOldVersions(installed);
            status?.Invoke(new("Service.FfmpegUpdated"));
            return LocalizationService.Get("Service.FfmpegUpToDate");
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ApprovedRelease> GetApprovedReleaseAsync(CancellationToken cancellationToken)
    {
        var json = await Http.GetStringAsync(ManifestUrl, cancellationToken);
        var manifest = JsonSerializer.Deserialize<DependencyManifest>(json, ManifestJsonOptions)
            ?? throw new LocalizedException("Service.FfmpegManifestEmpty");

        if (manifest.SchemaVersion != 1 || manifest.Ffmpeg is null)
            throw new LocalizedException("Service.FfmpegManifestIncompatible");

        var version = manifest.Ffmpeg.Version?.Trim() ?? string.Empty;
        var archiveUrl = manifest.Ffmpeg.ArchiveUrl?.Trim() ?? string.Empty;
        var sha256 = manifest.Ffmpeg.Sha256?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!IsSafeVersion(version))
            throw new LocalizedException("Service.FfmpegVersionInvalid");
        if (!IsAllowedArchiveUrl(archiveUrl))
            throw new LocalizedException("Service.FfmpegSourceInvalid");
        if (!IsSha256(sha256))
            throw new LocalizedException("Service.FfmpegShaInvalid");

        return new ApprovedRelease(version, archiveUrl, sha256);
    }

    private async Task<string> InstallReleaseAsync(
        ApprovedRelease release,
        Action<LocalizedMessage>? status,
        IProgress<double>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_baseDirectory);
        var targetDirectory = Path.Combine(_baseDirectory, release.Version);
        var tempZip = Path.Combine(_baseDirectory, $"ffmpeg-{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(_baseDirectory, $"extract-{Guid.NewGuid():N}");

        try
        {
            status?.Invoke(new("Service.FfmpegDownloading", release.Version));
            using var response = await Http.GetAsync(release.ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var destination = new FileStream(tempZip, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, true))
            {
                var buffer = new byte[131072];
                long written = 0;
                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken);
                    if (read == 0)
                        break;

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    written += read;
                    if (total is > 0)
                        progress?.Report(Math.Clamp(written * 100d / total.Value, 0, 100));
                }
            }

            status?.Invoke(new("Service.FfmpegVerifying"));
            var hash = await ComputeSha256Async(tempZip, cancellationToken);
            if (!string.Equals(hash, release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new LocalizedException("Service.FfmpegVerificationFailed");

            Directory.CreateDirectory(tempExtract);
            using (var archive = ZipFile.OpenRead(tempZip))
            {
                ExtractBinary(archive, "ffmpeg.exe", Path.Combine(tempExtract, "ffmpeg.exe"));
                ExtractBinary(archive, "ffprobe.exe", Path.Combine(tempExtract, "ffprobe.exe"));
            }

            if (!File.Exists(Path.Combine(tempExtract, "ffmpeg.exe")) || !File.Exists(Path.Combine(tempExtract, "ffprobe.exe")))
                throw new LocalizedException("Service.FfmpegArchiveMissingBinaries");

            Directory.CreateDirectory(targetDirectory);
            File.Move(Path.Combine(tempExtract, "ffmpeg.exe"), Path.Combine(targetDirectory, "ffmpeg.exe"), true);
            File.Move(Path.Combine(tempExtract, "ffprobe.exe"), Path.Combine(targetDirectory, "ffprobe.exe"), true);
            await File.WriteAllTextAsync(Path.Combine(targetDirectory, "SOURCE.txt"),
                $"{release.Version}{Environment.NewLine}{release.ArchiveUrl}{Environment.NewLine}sha256:{release.Sha256}{Environment.NewLine}manifest:{ManifestUrl}{Environment.NewLine}",
                cancellationToken);
            await WriteActiveVersionAsync(release.Version, cancellationToken);

            progress?.Report(100);
            status?.Invoke(new("Service.FfmpegReady"));
            return targetDirectory;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch { }
        }
    }

    private bool TryGetInstalledDirectory(out string directory)
    {
        directory = string.Empty;
        try
        {
            if (File.Exists(ActiveVersionPath))
            {
                var version = File.ReadAllText(ActiveVersionPath).Trim();
                if (IsSafeVersion(version))
                {
                    var active = Path.Combine(_baseDirectory, version);
                    if (HasExpectedBinaries(active))
                    {
                        directory = active;
                        return true;
                    }
                }
            }

            var bootstrap = Path.Combine(_baseDirectory, BootstrapVersion);
            if (HasExpectedBinaries(bootstrap))
            {
                directory = bootstrap;
                return true;
            }

            if (!Directory.Exists(_baseDirectory))
                return false;

            var candidate = Directory.EnumerateDirectories(_baseDirectory)
                .Where(path => !Path.GetFileName(path).StartsWith("extract-", StringComparison.OrdinalIgnoreCase))
                .Where(HasExpectedBinaries)
                .OrderByDescending(path => Directory.GetLastWriteTimeUtc(path))
                .FirstOrDefault();

            if (candidate is null)
                return false;

            directory = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task WriteActiveVersionAsync(string version, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_baseDirectory);
        var temp = Path.Combine(_baseDirectory, $"active-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temp, version + Environment.NewLine, cancellationToken);
            File.Move(temp, ActiveVersionPath, true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private void CleanupOldVersions(string currentDirectory)
    {
        try
        {
            if (!Directory.Exists(_baseDirectory))
                return;

            var currentFullPath = Path.GetFullPath(currentDirectory).TrimEnd(Path.DirectorySeparatorChar);
            foreach (var directory in Directory.EnumerateDirectories(_baseDirectory))
            {
                var fullPath = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
                if (string.Equals(fullPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try { Directory.Delete(directory, true); } catch { }
            }
        }
        catch { }
    }

    private static bool HasExpectedBinaries(string directory) =>
        File.Exists(Path.Combine(directory, "ffmpeg.exe")) &&
        File.Exists(Path.Combine(directory, "ffprobe.exe"));

    private static void ExtractBinary(ZipArchive archive, string fileName, string destination)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').EndsWith($"/bin/{fileName}", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new LocalizedException("Service.FfmpegFileMissing", fileName);
        entry.ExtractToFile(destination, true);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static ApprovedRelease GetBootstrapRelease() =>
        new(BootstrapVersion, BootstrapArchiveUrl, BootstrapSha256);

    private static bool IsSafeVersion(string version) =>
        version.Length is > 0 and <= 100 &&
        version.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '_' or '+' or '-');

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);

    private static bool IsAllowedArchiveUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttps
            && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.StartsWith("/BtbN/FFmpeg-Builds/releases/download/", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.Contains("win64-lgpl", StringComparison.OrdinalIgnoreCase)
            && uri.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClipPull/0.5.0 (+https://github.com/Sd-tech-Sol/ClipPull)");
        return client;
    }

    private sealed class DependencyManifest
    {
        public int SchemaVersion { get; set; }
        public FfmpegEntry? Ffmpeg { get; set; }
    }

    private sealed class FfmpegEntry
    {
        public string? Version { get; set; }
        public string? ArchiveUrl { get; set; }
        public string? Sha256 { get; set; }
    }

    private readonly record struct ApprovedRelease(string Version, string ArchiveUrl, string Sha256);
}
