namespace ClipPull.Models;

internal sealed record DownloadProgress(
    double Percent,
    double? BytesPerSecond,
    TimeSpan? Eta);
