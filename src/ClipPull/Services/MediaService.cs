using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClipPull.Models;

namespace ClipPull.Services;

internal sealed partial class MediaService
{
    public async Task<DownloadResult> DownloadAsync(
        string enginePath,
        string url,
        string outputDirectory,
        DownloadSettings settings,
        IProgress<double>? progress,
        Action<string>? status,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var outputTemplate = Path.Combine(outputDirectory, "%(title).180s [%(id)s].%(ext)s");
        var startInfo = BaseStartInfo(enginePath);

        Add(startInfo, "--ignore-config");
        Add(startInfo, "--no-plugin-dirs");
        Add(startInfo, "--newline");
        Add(startInfo, "--windows-filenames");
        Add(startInfo, "--no-overwrites");
        Add(startInfo, "--output", outputTemplate);
        Add(startInfo, "--progress-template", "download:CLIPPULL_PROGRESS=%(progress._percent_str)s");
        Add(startInfo, "--print", "after_move:CLIPPULL_FILE=%(filepath)s");

        if (settings.AllowPlaylists)
        {
            Add(startInfo, "--yes-playlist");
            Add(startInfo, "--playlist-items", $"1:{Math.Max(1, settings.PlaylistLimit)}");
            Add(startInfo, "--ignore-errors");
        }
        else
        {
            Add(startInfo, "--no-playlist");
        }

        if (settings.UseHistory && !string.IsNullOrWhiteSpace(settings.ArchivePath))
            Add(startInfo, "--download-archive", settings.ArchivePath);

        if (!string.IsNullOrWhiteSpace(settings.Browser))
            Add(startInfo, "--cookies-from-browser", settings.Browser.ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(settings.FfmpegDirectory))
            Add(startInfo, "--ffmpeg-location", settings.FfmpegDirectory);

        ConfigureFormat(startInfo, settings);
        Add(startInfo, url);

        var errors = new Queue<string>();
        var files = new List<string>();
        var archiveHit = false;

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("Impossible de démarrer yt-dlp.");

        using var cancellationRegistration = cancellationToken.Register(() => Kill(process));
        status?.Invoke("Analyse du lien...");

        var stdoutTask = PumpAsync(process.StandardOutput, line =>
        {
            if (line.StartsWith("CLIPPULL_FILE=", StringComparison.Ordinal))
            {
                var file = line["CLIPPULL_FILE=".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(file))
                    files.Add(file);
                return;
            }

            if (line.Contains("recorded in the archive", StringComparison.OrdinalIgnoreCase))
                archiveHit = true;

            var match = ProgressRegex().Match(line);
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                progress?.Report(Math.Clamp(percent, 0, 100));
                status?.Invoke($"Téléchargement... {percent:0.#}%");
            }
        }, cancellationToken);

        var stderrTask = PumpAsync(process.StandardError, line =>
        {
            if (line.Contains("recorded in the archive", StringComparison.OrdinalIgnoreCase))
                archiveHit = true;
            lock (errors)
            {
                errors.Enqueue(line);
                while (errors.Count > 20)
                    errors.Dequeue();
            }
        }, cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        await Task.WhenAll(stdoutTask, stderrTask);

        if (process.ExitCode != 0)
        {
            string detail;
            lock (errors)
                detail = string.Join(Environment.NewLine, errors.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (string.IsNullOrWhiteSpace(detail))
                detail = "yt-dlp a retourné une erreur sans détail.";
            throw new InvalidOperationException(detail);
        }

        progress?.Report(100);
        if (archiveHit && files.Count == 0)
            status?.Invoke("Déjà téléchargé selon l'historique local.");
        else if (files.Count > 1)
            status?.Invoke($"{files.Count} fichiers téléchargés.");

        return new DownloadResult(files.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<MediaPreview> PreviewAsync(
        string enginePath,
        string url,
        string? browser,
        CancellationToken cancellationToken)
    {
        var startInfo = BaseStartInfo(enginePath);
        Add(startInfo, "--ignore-config");
        Add(startInfo, "--no-plugin-dirs");
        Add(startInfo, "--no-warnings");
        Add(startInfo, "--skip-download");
        Add(startInfo, "--flat-playlist");
        Add(startInfo, "--playlist-items", "1:1");
        Add(startInfo, "--dump-single-json");
        if (!string.IsNullOrWhiteSpace(browser))
            Add(startInfo, "--cookies-from-browser", browser.ToLowerInvariant());
        Add(startInfo, url);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new InvalidOperationException("Impossible de démarrer yt-dlp.");

        using var cancellationRegistration = cancellationToken.Register(() => Kill(process));
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var json = await stdoutTask;
        var error = await stderrTask;

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? "Impossible d'obtenir l'aperçu." : error.Trim());

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var title = ReadString(root, "title") ?? "Sans titre";
        var platform = ReadString(root, "extractor_key") ?? ReadString(root, "extractor") ?? "Web";
        var thumbnail = ReadString(root, "thumbnail");
        var duration = ReadDouble(root, "duration");
        var playlistCount = ReadInt(root, "playlist_count");

        if (root.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array && entries.GetArrayLength() > 0)
        {
            var first = entries[0];
            thumbnail ??= ReadString(first, "thumbnail");
            duration ??= ReadDouble(first, "duration");
            if (title == "Sans titre")
                title = ReadString(first, "title") ?? title;
        }

        return new MediaPreview(title, platform, thumbnail, duration, playlistCount);
    }

    private static void ConfigureFormat(ProcessStartInfo startInfo, DownloadSettings settings)
    {
        if (settings.Mode is MediaMode.AudioM4a or MediaMode.AudioMp3)
        {
            Add(startInfo, "--format", "bestaudio/best");
            Add(startInfo, "--extract-audio");
            Add(startInfo, "--audio-format", settings.Mode == MediaMode.AudioMp3 ? "mp3" : "m4a");
            Add(startInfo, "--audio-quality", "0");
            return;
        }

        var format = settings.Quality switch
        {
            VideoQuality.Best => "bestvideo[ext=mp4]+bestaudio[ext=m4a]/best[ext=mp4]/best",
            VideoQuality.P1080 => "bestvideo[height<=1080][ext=mp4]+bestaudio[ext=m4a]/best[height<=1080][ext=mp4]/best[height<=1080]",
            VideoQuality.P720 => "bestvideo[height<=720][ext=mp4]+bestaudio[ext=m4a]/best[height<=720][ext=mp4]/best[height<=720]",
            VideoQuality.P480 => "bestvideo[height<=480][ext=mp4]+bestaudio[ext=m4a]/best[height<=480][ext=mp4]/best[height<=480]",
            VideoQuality.Small => "worst[ext=mp4]/worst",
            _ => "best[ext=mp4]/best"
        };
        Add(startInfo, "--format", format);
        if (settings.Quality is VideoQuality.Best or VideoQuality.P1080 or VideoQuality.P720 or VideoQuality.P480)
            Add(startInfo, "--merge-output-format", "mp4/mkv");
    }

    private static ProcessStartInfo BaseStartInfo(string enginePath) => new()
    {
        FileName = enginePath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };

    private static async Task PumpAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;
            onLine(line);
        }
    }

    private static void Kill(Process process)
    {
        try { if (!process.HasExited) process.Kill(true); } catch { }
    }

    private static void Add(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static double? ReadDouble(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetDouble(out var number) ? number : null;

    private static int? ReadInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.TryGetInt32(out var number) ? number : null;

    [GeneratedRegex(@"CLIPPULL_PROGRESS=\s*([0-9]+(?:\.[0-9]+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex ProgressRegex();
}
