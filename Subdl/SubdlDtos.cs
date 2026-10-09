using System.Text.Json.Serialization;

namespace MediaPager.Plugins.Subtitles.Subdl.Subdl;

internal sealed record SubdlSearchEnvelope(
    [property: JsonPropertyName("status")] bool? Status,
    [property: JsonPropertyName("error")] string? Error,
    [property: JsonPropertyName("subtitles")] SubdlSubtitleRow[]? Subtitles);

internal sealed record SubdlSubtitleRow(
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("release_name")] string? ReleaseName,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("hi")] bool? HearingImpaired,
    [property: JsonPropertyName("download_count")] long? DownloadCount);
