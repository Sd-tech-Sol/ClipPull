using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ClipPull.Localization;

namespace ClipPull.Services;

internal sealed partial class DownloadService
{
    public async Task<string?> DownloadAsync(
        string enginePath,
        string url,
        string outputDirectory,
        string? browser,
        IProgress<double>? progress,
        Action<LocalizedMessage>? status,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var startedUtc = DateTime.UtcNow;
        var outputTemplate = Path.Combine(outputDirectory, "%(title).180s [%(id)s].%(ext)s");

        var startInfo = new ProcessStartInfo
        {
            FileName = enginePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        Add(startInfo, "--ignore-config");
        Add(startInfo, "--no-plugin-dirs");
        Add(startInfo, "--no-playlist");
        Add(startInfo, "--newline");
        Add(startInfo, "--progress");
        Add(startInfo, "--windows-filenames");
        Add(startInfo, "--no-overwrites");
        Add(startInfo, "--format", "best[ext=mp4]/best");
        Add(startInfo, "--output", outputTemplate);
        Add(startInfo, "--progress-template", "download:CLIPPULL_PROGRESS=%(progress._percent_str)s");
        Add(startInfo, "--print", "after_move:CLIPPULL_FILE=%(filepath)s");

        if (!string.IsNullOrWhiteSpace(browser))
            Add(startInfo, "--cookies-from-browser", browser.ToLowerInvariant());

        Add(startInfo, url);

        var errors = new Queue<string>();
        string? discoveredFile = null;

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
            throw new LocalizedException("Service.StartYtDlpFailed");

        using var cancellationRegistration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Best effort cancellation.
            }
        });

        status?.Invoke(new("Status.AnalyzingLink"));

        var stdoutTask = PumpStdoutAsync(process.StandardOutput, line =>
        {
            if (line.StartsWith("CLIPPULL_FILE=", StringComparison.Ordinal))
            {
                discoveredFile = line["CLIPPULL_FILE=".Length..].Trim();
                return;
            }

            var match = ProgressRegex().Match(line);
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                progress?.Report(Math.Clamp(percent, 0, 100));
                status?.Invoke(new("Queue.DownloadingProgress", percent));
            }
        }, cancellationToken);

        var stderrTask = PumpStdoutAsync(process.StandardError, line =>
        {
            var match = ProgressRegex().Match(line);
            if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent))
            {
                progress?.Report(Math.Clamp(percent, 0, 100));
                status?.Invoke(new("Queue.DownloadingProgress", percent));
                return;
            }

            lock (errors)
            {
                errors.Enqueue(line);
                while (errors.Count > 15)
                    errors.Dequeue();
            }
        }, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await Task.WhenAll(stdoutTask, stderrTask);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        if (process.ExitCode != 0)
        {
            string detail;
            lock (errors)
                detail = string.Join(Environment.NewLine, errors);

            if (string.IsNullOrWhiteSpace(detail))
                throw new LocalizedException("Service.YtDlpNoDetail");

            throw new InvalidOperationException(detail);
        }

        progress?.Report(100);

        if (!string.IsNullOrWhiteSpace(discoveredFile) && File.Exists(discoveredFile))
            return discoveredFile;

        return Directory.EnumerateFiles(outputDirectory)
            .Select(path => new FileInfo(path))
            .Where(file => file.LastWriteTimeUtc >= startedUtc.AddSeconds(-2))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => file.FullName)
            .FirstOrDefault();
    }

    private static async Task PumpStdoutAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            onLine(line);
        }
    }

    private static void Add(ProcessStartInfo startInfo, params string[] arguments)
    {
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);
    }

    [GeneratedRegex(@"CLIPPULL_PROGRESS=\s*([0-9]+(?:\.[0-9]+)?)%", RegexOptions.CultureInvariant)]
    private static partial Regex ProgressRegex();
}
