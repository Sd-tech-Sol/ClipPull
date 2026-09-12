namespace ClipPull.Models;

internal enum UpdateCheckStatus
{
    UpToDate,
    UpdateAvailable,
    Failed
}

internal sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    string? LatestVersion = null,
    Uri? ReleasePage = null);
