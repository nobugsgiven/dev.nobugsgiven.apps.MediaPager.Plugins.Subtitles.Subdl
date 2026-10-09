# MediaPager.Plugins.Subtitles.Subdl

Official **Subdl subtitle provider** plugin for MediaPager. References the plugin SDK
(`MediaPager.App.PluginContracts`) only.

Plugin id: `mediapager.subtitles.subdl` · capability: `subtitles`

## What it does

- `ISubtitleProviderPlugin` — subtitle search via `api.subdl.com`, subtitle file retrieval via
  `dl.subdl.com` (zip), with an SDK-appropriate `FileId` path check. Returns native SRT or
  VTT; the host converts SRT→WebVTT for browser tracks.
- `IPluginSettingsSchema` — data-driven settings.

## Settings (`plugins.subdl.*`)

| Key | Type | Notes |
|---|---|---|
| `apiKey` | password, required, secret | Subdl API key. |

## Layout

- `SubdlProviderPlugin.cs` — the plugin class.
- `Subdl/` — API client + DTOs (`SubdlApi`, `SubdlDtos`).

## Building

```sh
dotnet build MediaPager.Plugins.Subtitles.Subdl/MediaPager.Plugins.Subtitles.Subdl.csproj
```
