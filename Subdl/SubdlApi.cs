using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MediaPager.App.PluginContracts;

namespace MediaPager.Plugins.Subtitles.Subdl.Subdl;

internal sealed record SubdlHit(
    string FileId,
    string? Name,
    string? Release,
    string? Language,
    bool HearingImpaired,
    long Downloads);

internal sealed record SubdlDocument(string Content, SubtitleFormat Format);

/// <summary>
/// Subdl API v1 wrapper. Searches hit api.subdl.com; subtitle archives come from dl.subdl.com.
/// The FileId is an opaque path (e.g. "subtitle/3703456-8624437.zip") — it is
/// opaque to the host and validated here before the URL is built.
/// </summary>
internal sealed class SubdlApi
{
    private const string BaseUrl = "https://api.subdl.com/api/v1";
    private const string FileHostBase = "https://dl.subdl.com";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<SubdlHit>> SearchAsync(
        string apiKey,
        string language,
        string? imdbId,
        string? tmdbId,
        string? query,
        bool isEpisode,
        int? season,
        int? episode,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string?>
        {
            ["api_key"] = apiKey,
            ["languages"] = language,
            ["type"] = isEpisode ? "tv" : "movie",
            ["subs_per_page"] = "30",
            ["hi"] = "1",
            ["imdb_id"] = imdbId,
            ["tmdb_id"] = tmdbId,
            ["film_name"] = query,
        };
        if (isEpisode)
        {
            parameters["season_number"] = season?.ToString();
            parameters["episode_number"] = episode?.ToString();
        }

        using var response = await _http.GetAsync($"{BaseUrl}/subtitles?{Query(parameters)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return [];

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var envelope = await JsonSerializer.DeserializeAsync<SubdlSearchEnvelope>(stream, _json, cancellationToken);
        if (envelope is null || envelope.Status != true || envelope.Subtitles is null)
            return [];

        return envelope.Subtitles
            .Where(row => !string.IsNullOrWhiteSpace(row.Url))
            .Select(row =>
            {
                var fileId = row.Url!.Split('?', '#')[0].TrimStart('/');
                return new SubdlHit(
                    fileId,
                    row.Name,
                    row.ReleaseName,
                    row.Language ?? language,
                    row.HearingImpaired ?? false,
                    row.DownloadCount ?? 0);
            })
            .ToList();
    }

    public async Task<SubdlDocument?> FetchAsync(string apiKey, string fileId, CancellationToken cancellationToken)
    {
        var safePath = fileId.TrimStart('/');
        if (!IsSafeFilePath(safePath))
            return null;

        using var response = await _http.GetAsync(
            $"{FileHostBase}/{safePath}?api_key={Uri.EscapeDataString(apiKey)}",
            cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var zipStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault(entry =>
            entry.Name.EndsWith(".srt", StringComparison.OrdinalIgnoreCase) ||
            entry.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase)) ??
            archive.Entries.FirstOrDefault(entry => entry.Length > 0);
        if (entry is null)
            return null;

        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var format = entry.Name.EndsWith(".vtt", StringComparison.OrdinalIgnoreCase)
            ? SubtitleFormat.Vtt
            : SubtitleFormat.Srt;

        return new SubdlDocument(content, format);
    }

    private static bool IsSafeFilePath(string path) =>
        path.StartsWith("subtitle/", StringComparison.OrdinalIgnoreCase)
        && !path.Contains("..")
        && path.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '/' or '-' or '_');

    private static string Query(Dictionary<string, string?> parameters) =>
        string.Join("&", parameters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value!)}"));
}
