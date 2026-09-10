using System.IO.Compression;
using System.Security.Cryptography;

namespace ClipPull.Services;

internal sealed class FfmpegManager
{
    // Pinned, reviewed asset: BtbN/FFmpeg-Builds, FFmpeg 9.0 branch, Windows x64 LGPL static.
    private const string VersionId = "n9.0.1-26-g5c8e7e2433";
    private const string ArchiveUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-09-06-13-06/ffmpeg-n9.0.1-26-g5c8e7e2433-win64-lgpl-9.0.zip";
    private const string ArchiveSha256 = "4700c0bcb523466fdf5e36e22ad4ff3fadf33f203e2dbfdc78f5b4cd068b8818";

    private static readonly HttpClient Http = CreateClient();
    private readonly string _directory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull", "ffmpeg", VersionId);

    public string FfmpegPath => Path.Combine(_directory, "ffmpeg.exe");
    public string FfprobePath => Path.Combine(_directory, "ffprobe.exe");
    public string DirectoryPath => _directory;
    public bool IsInstalled => File.Exists(FfmpegPath) && File.Exists(FfprobePath);

    public async Task<string> EnsureAsync(Action<string>? status, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (IsInstalled)
        {
            status?.Invoke("FFmpeg local prêt.");
            return _directory;
        }

        var baseDirectory = Path.GetDirectoryName(_directory)!;
        Directory.CreateDirectory(baseDirectory);
        var tempZip = Path.Combine(baseDirectory, $"ffmpeg-{Guid.NewGuid():N}.zip");
        var tempExtract = Path.Combine(baseDirectory, $"extract-{Guid.NewGuid():N}");

        try
        {
            status?.Invoke("Téléchargement de FFmpeg vérifié (~140 Mo)...");
            using var response = await Http.GetAsync(ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
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

            status?.Invoke("Vérification SHA-256 de FFmpeg...");
            var hash = await ComputeSha256Async(tempZip, cancellationToken);
            if (!string.Equals(hash, ArchiveSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La vérification SHA-256 de FFmpeg a échoué. Rien n'a été installé.");

            Directory.CreateDirectory(tempExtract);
            using (var archive = ZipFile.OpenRead(tempZip))
            {
                ExtractBinary(archive, "ffmpeg.exe", Path.Combine(tempExtract, "ffmpeg.exe"));
                ExtractBinary(archive, "ffprobe.exe", Path.Combine(tempExtract, "ffprobe.exe"));
            }

            if (!File.Exists(Path.Combine(tempExtract, "ffmpeg.exe")) || !File.Exists(Path.Combine(tempExtract, "ffprobe.exe")))
                throw new InvalidOperationException("L'archive FFmpeg vérifiée ne contient pas les exécutables attendus.");

            Directory.CreateDirectory(_directory);
            File.Move(Path.Combine(tempExtract, "ffmpeg.exe"), FfmpegPath, true);
            File.Move(Path.Combine(tempExtract, "ffprobe.exe"), FfprobePath, true);
            await File.WriteAllTextAsync(Path.Combine(_directory, "SOURCE.txt"),
                $"{VersionId}{Environment.NewLine}{ArchiveUrl}{Environment.NewLine}sha256:{ArchiveSha256}{Environment.NewLine}", cancellationToken);

            progress?.Report(100);
            status?.Invoke("FFmpeg prêt.");
            return _directory;
        }
        finally
        {
            try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
            try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch { }
        }
    }

    private static void ExtractBinary(ZipArchive archive, string fileName, string destination)
    {
        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').EndsWith($"/bin/{fileName}", StringComparison.OrdinalIgnoreCase));
        if (entry is null)
            throw new InvalidOperationException($"{fileName} est absent de l'archive FFmpeg.");
        entry.ExtractToFile(destination, true);
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClipPull/0.3 (+https://github.com/Sd-tech-Sol/ClipPull)");
        return client;
    }
}
