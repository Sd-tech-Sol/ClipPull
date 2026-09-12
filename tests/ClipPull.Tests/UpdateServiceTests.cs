using System.Net;
using ClipPull.Models;
using ClipPull.Services;
using ClipPull.ViewModels;

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

    [Fact]
    public async Task FailedAutomaticCheckDoesNotRecordLastCheckTimestamp()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ClipPull-update-tests-{Guid.NewGuid():N}");
        try
        {
            var settingsService = new SettingsService(directory);
            var client = new HttpClient(new StubHandler(_ => throw new HttpRequestException("offline")));
            using var viewModel = new MainViewModel(settingsService, new UpdateService(client));

            await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

            Assert.Null(settingsService.Load().LastUpdateCheckUtc);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SuccessfulCheckRecordsLastCheckTimestamp()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ClipPull-update-tests-{Guid.NewGuid():N}");
        try
        {
            var settingsService = new SettingsService(directory);
            var client = new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"tag_name":"v0.6.0","html_url":"https://github.com/Sd-tech-Sol/ClipPull/releases/tag/v0.6.0"}""")
            }));
            using var viewModel = new MainViewModel(settingsService, new UpdateService(client));

            await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

            Assert.NotNull(settingsService.Load().LastUpdateCheckUtc);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancelUpdateChecksStopsAnInFlightCheckWithoutThrowing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ClipPull-update-tests-{Guid.NewGuid():N}");
        try
        {
            var gate = new TaskCompletionSource();
            var client = new HttpClient(new GatedHandler(gate));
            using var viewModel = new MainViewModel(new SettingsService(directory), new UpdateService(client));

            var checkTask = viewModel.CheckForUpdatesCommand.ExecuteAsync(null);
            viewModel.CancelUpdateChecks();

            await checkTask;

            Assert.False(viewModel.IsCheckingForUpdates);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
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

    private sealed class GatedHandler(TaskCompletionSource gate) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.WhenAny(gate.Task, Task.Delay(Timeout.Infinite, cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }
}
