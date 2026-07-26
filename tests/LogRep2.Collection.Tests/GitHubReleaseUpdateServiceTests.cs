using System.Net;
using System.Net.Http;
using System.Text;
using FfxiTempLogCollector.App;

namespace FfxiTempLogCollector.Tests;

public sealed class GitHubReleaseUpdateServiceTests
{
    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("v1.2.3-beta.1", 1, 2, 3)]
    [InlineData("1.2.3+build", 1, 2, 3)]
    public void バージョン文字列を解析できる(
        string value,
        int major,
        int minor,
        int build)
    {
        var success = GitHubReleaseUpdateService.TryParseVersion(
            value,
            out var version);

        Assert.True(success);
        Assert.Equal(new Version(major, minor, build, 0), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("release")]
    [InlineData("v1")]
    public void 不正なバージョン文字列を拒否する(string value)
    {
        Assert.False(
            GitHubReleaseUpdateService.TryParseVersion(value, out _));
    }

    [Fact]
    public async Task 新しい安定版を検出する()
    {
        var current = GitHubReleaseUpdateService.CurrentVersion;
        var latest = new Version(
            current.Major + 1,
            0,
            0,
            0);
        using var client = CreateClient(
            $$"""
              {
                "tag_name": "v{{latest.ToString(3)}}",
                "name": "LogRep2 v{{latest.ToString(3)}}",
                "html_url": "https://github.com/th0811/LogRep2/releases/tag/v{{latest.ToString(3)}}",
                "draft": false,
                "prerelease": false
              }
              """);
        var service = new GitHubReleaseUpdateService(client);

        var result = await service.CheckAsync();

        Assert.NotNull(result);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal(latest, result.LatestVersion);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task 下書きとプレリリースを通知しない(
        bool draft,
        bool prerelease)
    {
        using var client = CreateClient(
            $$"""
              {
                "tag_name": "v99.0.0",
                "name": "LogRep2 v99.0.0",
                "html_url": "https://github.com/th0811/LogRep2/releases/tag/v99.0.0",
                "draft": {{draft.ToString().ToLowerInvariant()}},
                "prerelease": {{prerelease.ToString().ToLowerInvariant()}}
              }
              """);
        var service = new GitHubReleaseUpdateService(client);

        Assert.Null(await service.CheckAsync());
    }

    [Fact]
    public async Task 現在と同じバージョンを更新扱いにしない()
    {
        var current = GitHubReleaseUpdateService.CurrentVersion;
        using var client = CreateClient(
            $$"""
              {
                "tag_name": "v{{current.ToString(3)}}",
                "name": "LogRep2",
                "html_url": "https://github.com/th0811/LogRep2/releases/latest",
                "draft": false,
                "prerelease": false
              }
              """);
        var service = new GitHubReleaseUpdateService(client);

        var result = await service.CheckAsync();

        Assert.NotNull(result);
        Assert.False(result.IsUpdateAvailable);
    }

    private static HttpClient CreateClient(string responseJson)
    {
        return new HttpClient(new StubHttpMessageHandler(responseJson));
    }

    private sealed class StubHttpMessageHandler(string responseJson)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(
                GitHubReleaseUpdateService.LatestReleaseApiUri,
                request.RequestUri);
            Assert.NotEmpty(request.Headers.UserAgent);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    responseJson,
                    Encoding.UTF8,
                    "application/json"),
            });
        }
    }
}
