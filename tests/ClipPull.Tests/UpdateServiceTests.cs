using System.Net;
using ClipPull.Models;
using ClipPull.Services;

namespace ClipPull.Tests;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task NewerOfficialReleaseIsReported()
    {
        var service = CreateService(HttpStatusCode.OK,
            """{"tag_name":"v0.6.1","html_url":"https://github.com/Sd-tech-Sol/ClipPull/releases/tag/v0.6.1"}""");

        var result = await service.CheckAsync("0.6.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("0.6.1", result.LatestVersion);
        Assert.Equal("https://github.com/Sd-tech-Sol/ClipPull/releases/tag/v0.6.1", result.ReleasePage?.AbsoluteUri);
    }

    [Fact]
    public async Task CurrentVersionIsUpToDate()
    {
        var service = CreateService(HttpStatusCode.OK,
            """{"tag_name":"v0.6.0","html_url":"https://github.com/Sd-tech-Sol/ClipPull/releases/tag/v0.6.0"}""");

        var result = await service.CheckAsync("0.6.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
    }

    [Theory]
    [InlineData("{bad json")]
    [InlineData("{\"tag_name\":\"v0.6.1\",\"html_url\":\"https://evil.example/release\"}")]
    [InlineData("{\"tag_name\":\"not-semver\",\"html_url\":\"https://github.com/Sd-tech-Sol/ClipPull/releases/tag/not-semver\"}")]
    public async Task InvalidResponsesFailSafely(string json)
    {
        var result = await CreateService(HttpStatusCode.OK, json)
            .CheckAsync("0.6.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public async Task NetworkFailureFailsSafely()
    {
        var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));
        var result = await new UpdateService(client).CheckAsync("0.6.0", CancellationToken.None);

        Assert.Equal(UpdateCheckStatus.Failed, result.Status);
    }

    [Fact]
    public void AutomaticIntervalHonorsOptOutAndEighteenHours()
    {
        var now = new DateTimeOffset(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
        var interval = TimeSpan.FromHours(18);

        Assert.False(UpdateService.ShouldRunAutomaticCheck(false, null, now, interval));
        Assert.False(UpdateService.ShouldRunAutomaticCheck(true, now.AddHours(-17), now, interval));
        Assert.True(UpdateService.ShouldRunAutomaticCheck(true, now.AddHours(-18), now, interval));
        Assert.True(UpdateService.ShouldRunAutomaticCheck(true, null, now, interval));
    }

    private static UpdateService CreateService(HttpStatusCode status, string json) =>
        new(new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(json)
        })));

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
