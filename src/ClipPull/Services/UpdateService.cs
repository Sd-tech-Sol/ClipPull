using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClipPull.Models;

namespace ClipPull.Services;

internal sealed partial class UpdateService
{
    internal static readonly Uri LatestReleaseApi = new(
        "https://api.github.com/repos/Sd-tech-Sol/ClipPull/releases/latest");

    private readonly HttpClient _httpClient;

    public UpdateService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
    }

    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken cancellationToken)
    {
        if (!TryParseVersion(currentVersion, out var current))
            return new UpdateCheckResult(UpdateCheckStatus.Failed);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("ClipPull", currentVersion));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

            using var response = await _httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new UpdateCheckResult(UpdateCheckStatus.Failed);

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var tag = ReadString(root, "tag_name");
            var pageText = ReadString(root, "html_url");
            if (!TryParseVersion(tag, out var latest) || !TryValidateReleasePage(pageText, tag!, out var releasePage))
                return new UpdateCheckResult(UpdateCheckStatus.Failed);

            var displayVersion = $"{latest.Major}.{latest.Minor}.{latest.Build}";
            return latest > current
                ? new UpdateCheckResult(UpdateCheckStatus.UpdateAvailable, displayVersion, releasePage)
                : new UpdateCheckResult(UpdateCheckStatus.UpToDate, displayVersion, releasePage);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
        catch (HttpRequestException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
        catch (JsonException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
        catch (IOException)
        {
            return new UpdateCheckResult(UpdateCheckStatus.Failed);
        }
    }

    internal static bool ShouldRunAutomaticCheck(
        bool enabled,
        DateTimeOffset? lastCheckUtc,
        DateTimeOffset nowUtc,
        TimeSpan interval) =>
        enabled && (lastCheckUtc is null || nowUtc - lastCheckUtc.Value >= interval);

    internal static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = VersionRegex().Match(value.Trim());
        return match.Success && Version.TryParse(match.Groups[1].Value, out version!);
    }

    internal static bool TryValidateReleasePage(string? value, string tag, out Uri releasePage)
    {
        releasePage = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var candidate) ||
            candidate.Scheme != Uri.UriSchemeHttps ||
            !string.Equals(candidate.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !candidate.IsDefaultPort || !string.IsNullOrEmpty(candidate.Query) || !string.IsNullOrEmpty(candidate.Fragment))
        {
            return false;
        }

        var expectedPath = $"/Sd-tech-Sol/ClipPull/releases/tag/{Uri.EscapeDataString(tag)}";
        if (!string.Equals(candidate.AbsolutePath.TrimEnd('/'), expectedPath, StringComparison.OrdinalIgnoreCase))
            return false;

        releasePage = candidate;
        return true;
    }

    private static string? ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    [GeneratedRegex(@"^v?(\d+\.\d+\.\d+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionRegex();
}
