using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using ClipPull.Localization;

namespace ClipPull.Services;

internal sealed partial class YtDlpManager
{
    private const string BinaryUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
    private const string ChecksumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";

    private static readonly HttpClient Http = CreateClient();

    private readonly string _engineDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClipPull",
        "bin");

    public string EnginePath => Path.Combine(_engineDirectory, "yt-dlp.exe");
    private string HashPath => Path.Combine(_engineDirectory, "yt-dlp.sha256");

    public async Task<string> EnsureAsync(Action<LocalizedMessage>? status, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_engineDirectory);

        string? latestHash;
        try
        {
            status?.Invoke(new("Service.YtDlpChecking"));
            var sums = await Http.GetStringAsync(ChecksumsUrl, cancellationToken);
            latestHash = ParseYtDlpHash(sums);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            if (await HasValidCachedEngineAsync(cancellationToken))
            {
                status?.Invoke(new("Service.YtDlpLocalVerified"));
                return EnginePath;
            }

            throw new LocalizedException("Service.YtDlpUnavailable");
        }

        if (latestHash is null)
            throw new LocalizedException("Service.YtDlpChecksumReadFailed");

        if (File.Exists(EnginePath) && File.Exists(HashPath))
        {
            var storedHash = (await File.ReadAllTextAsync(HashPath, cancellationToken)).Trim();
            if (string.Equals(storedHash, latestHash, StringComparison.OrdinalIgnoreCase))
            {
                var actualHash = await ComputeSha256Async(EnginePath, cancellationToken);
                if (string.Equals(actualHash, latestHash, StringComparison.OrdinalIgnoreCase))
                {
                    status?.Invoke(new("Service.YtDlpUpToDate"));
                    return EnginePath;
                }
            }
        }

        status?.Invoke(new("Service.YtDlpDownloading"));
        var tempPath = Path.Combine(_engineDirectory, $"yt-dlp-{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var source = await Http.GetStreamAsync(BinaryUrl, cancellationToken))
            await using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            status?.Invoke(new("Service.YtDlpVerifying"));
            var downloadedHash = await ComputeSha256Async(tempPath, cancellationToken);
            if (!string.Equals(downloadedHash, latestHash, StringComparison.OrdinalIgnoreCase))
                throw new LocalizedException("Service.YtDlpVerificationFailed");

            File.Move(tempPath, EnginePath, overwrite: true);
            await File.WriteAllTextAsync(HashPath, latestHash, cancellationToken);
            status?.Invoke(new("Service.YtDlpReady"));
            return EnginePath;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private async Task<bool> HasValidCachedEngineAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(EnginePath) || !File.Exists(HashPath))
            return false;

        try
        {
            var storedHash = (await File.ReadAllTextAsync(HashPath, cancellationToken)).Trim();
            if (!HashRegex().IsMatch(storedHash))
                return false;

            var actualHash = await ComputeSha256Async(EnginePath, cancellationToken);
            return string.Equals(actualHash, storedHash, StringComparison.OrdinalIgnoreCase);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static string? ParseYtDlpHash(string checksums)
    {
        foreach (var line in checksums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.EndsWith("yt-dlp.exe", StringComparison.OrdinalIgnoreCase))
                continue;

            var match = HashRegex().Match(line);
            if (match.Success)
                return match.Value.ToLowerInvariant();
        }

        return null;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        using var sha = SHA256.Create();
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(3)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ClipPull/0.5.0 (+https://github.com/Sd-tech-Sol/ClipPull)");
        return client;
    }

    [GeneratedRegex("^[a-fA-F0-9]{64}$|[a-fA-F0-9]{64}", RegexOptions.CultureInvariant)]
    private static partial Regex HashRegex();
}
