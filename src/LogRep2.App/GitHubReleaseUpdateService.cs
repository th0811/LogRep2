using System.Net.Http.Headers;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FfxiTempLogCollector.App;

internal sealed class GitHubReleaseUpdateService
{
    internal static readonly Uri LatestReleaseApiUri = new(
        "https://api.github.com/repos/th0811/LogRep2/releases/latest");

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public GitHubReleaseUpdateService(HttpClient httpClient)
    {
        _httpClient = httpClient
            ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<UpdateCheckResult?> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            LatestReleaseApiUri);
        request.Headers.UserAgent.Add(
            new ProductInfoHeaderValue("LogRep2", CurrentVersion.ToString(3)));
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var content = await response.Content.ReadAsStreamAsync(
            cancellationToken);
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(
            content,
            JsonOptions,
            cancellationToken);

        if (release is null
            || release.Draft
            || release.Prerelease
            || !TryParseVersion(release.TagName, out var latestVersion)
            || !TryValidateReleaseUri(release.HtmlUrl, out var releaseUri))
        {
            return null;
        }

        var currentVersion = CurrentVersion;
        return new UpdateCheckResult(
            currentVersion,
            latestVersion,
            release.Name,
            releaseUri,
            latestVersion > currentVersion);
    }

    internal static Version CurrentVersion
    {
        get
        {
            var assembly = typeof(GitHubReleaseUpdateService).Assembly;
            var informationalVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            return TryParseVersion(informationalVersion, out var version)
                ? version
                : assembly.GetName().Version ?? new Version(0, 0, 0);
        }
    }

    internal static bool TryParseVersion(
        string? value,
        out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var suffixIndex = normalized.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
        {
            normalized = normalized[..suffixIndex];
        }

        if (!Version.TryParse(normalized, out var parsed)
            || parsed.Major < 0
            || parsed.Minor < 0)
        {
            return false;
        }

        version = new Version(
            parsed.Major,
            parsed.Minor,
            Math.Max(parsed.Build, 0),
            Math.Max(parsed.Revision, 0));
        return true;
    }

    private static bool TryValidateReleaseUri(
        string? value,
        out Uri releaseUri)
    {
        releaseUri = null!;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed)
            || parsed.Scheme != Uri.UriSchemeHttps
            || !string.Equals(
                parsed.Host,
                "github.com",
                StringComparison.OrdinalIgnoreCase)
            || !parsed.AbsolutePath.StartsWith(
                "/th0811/LogRep2/releases/",
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        releaseUri = parsed;
        return true;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; init; }
    }
}

internal sealed record UpdateCheckResult(
    Version CurrentVersion,
    Version LatestVersion,
    string? ReleaseName,
    Uri ReleaseUri,
    bool IsUpdateAvailable);
