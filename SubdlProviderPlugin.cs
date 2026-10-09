using MediaPager.App.PluginContracts;
using MediaPager.Plugins.Subtitles.Subdl.Subdl;

namespace MediaPager.Plugins.Subtitles.Subdl;

/// <summary>
/// Official Subdl subtitle provider: search by IMDb/TMDb id or film name, with subtitle packs
/// (including hearing-impaired) from api.subdl.com. File retrieval returns native content from the
/// subtitle zip — the host converts SRT to WebVTT for browser tracks.
/// </summary>
public sealed class SubdlProviderPlugin(IPluginSettingsStore settingsStore) :
    IMediaPagerPlugin, IPluginSettingsSchema, ISubtitleProviderPlugin
{
    public const string PluginKey = "subdl";
    public const string ApiKeySetting = "apiKey";

    public const string SourceKey = "mediapager.subtitles.subdl";

    public PluginDescriptor Descriptor { get; } = new(
        Id: SourceKey,
        Name: "Subdl",
        Version: "0.1.0",
        Author: "Nobugsgiven",
        Description: "Official Subdl provider for movie and TV subtitles.");

    public IReadOnlyList<PluginSettingDefinition> Settings { get; } =
    [
        new PluginSettingDefinition(
            ApiKeySetting,
            "Subdl API key",
            PluginSettingType.Password,
            Required: true,
            Secret: true),
    ];

    private readonly SubdlApi _api = new();

    public async Task<IReadOnlyList<SubtitleHit>> SearchAsync(SubtitleRequest request, CancellationToken cancellationToken)
    {
        var hasImdb = !string.IsNullOrWhiteSpace(request.ImdbId);
        var hasTmdb = !string.IsNullOrWhiteSpace(request.TmdbId);
        var query = request.Query?.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        if ((hasImdb || hasTmdb || hasQuery) is false)
            return [];
        if (query is { Length: > 200 })
            return [];
        if ((request.Season != null) != (request.Episode != null))
            return [];
        if (request.Season is < 1 || request.Episode is < 1)
            return [];

        var apiKey = await ApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            return [];

        var isEpisode = request.Season is not null;
        var language = request.Language.ToUpperInvariant();
        var hits = await _api.SearchAsync(
            apiKey,
            language,
            DigitsOnly(request.ImdbId),
            request.TmdbId,
            query,
            isEpisode,
            request.Season,
            request.Episode,
            cancellationToken);

        return hits
            .Select(hit => new SubtitleHit(
                FileId: hit.FileId,
                Language: hit.Language ?? language,
                FileName: hit.Name,
                Release: hit.Release,
                HearingImpaired: hit.HearingImpaired,
                Popularity: hit.Downloads))
            .ToList();
    }

    public async Task<SubtitleDocument?> FetchAsync(SubtitleFetchRequest request, CancellationToken cancellationToken)
    {
        var apiKey = await ApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        return await _api.FetchAsync(apiKey, request.FileId, cancellationToken) is { } document
            ? new SubtitleDocument(document.Content, document.Format)
            : null;
    }

    private async Task<string?> ApiKeyAsync(CancellationToken cancellationToken) =>
        (await settingsStore.GetAsync(PluginKey, ApiKeySetting, cancellationToken))?.Trim();

    private static string? DigitsOnly(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new string(value.Where(char.IsDigit).ToArray());
}
