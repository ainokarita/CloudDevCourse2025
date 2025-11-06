using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text.Json;

namespace UmbracoProject.Services
{
    public record GiphyDto(string Title, string Url, int Width, int Height);

    public class GiphyService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;

        public GiphyService(HttpClient http, IConfiguration cfg)
        {
            _http = http;
            _cfg = cfg;
        }

        public async Task<GiphyDto?> GetRandomAsync(string? tag = null, string? rating = null, CancellationToken ct = default)
        {
            var key = _cfg["GIPHY_API_KEY"];
            if (string.IsNullOrWhiteSpace(key)) return null;

            tag ??= _cfg["GIPHY_DEFAULT_TAG"] ?? "coding";
            rating ??= _cfg["GIPHY_RATING"] ?? "g";

            var randomId = Guid.NewGuid().ToString("N"); // helps diversify results on GIPHY side

            // 1) Try SEARCH for relevance + variety
            var searchUrl = BuildAbsoluteUrl(
                $"gifs/search?api_key={Uri.EscapeDataString(key)}&q={Uri.EscapeDataString(tag)}&limit=50&rating={Uri.EscapeDataString(rating)}&random_id={randomId}"
            );

            using (var searchRes = await _http.GetAsync(searchUrl, ct))
            {
                if (searchRes.IsSuccessStatusCode)
                {
                    var searchJson = await searchRes.Content.ReadAsStringAsync(ct);
                    using var searchDoc = JsonDocument.Parse(searchJson);
                    if (searchDoc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > 0)
                    {
                        var idx = RandomNumberGenerator.GetInt32(arr.GetArrayLength());
                        var pick = arr[idx];
                        var dto = MapToDto(pick);
                        if (dto != null) return dto;
                    }
                }
            }

            // 2) Fallback to RANDOM with a broader tag list to reduce repeats
            var fallbacks = new[] { tag, "developer", "programming", "open source", "tech", "coding" };
            foreach (var t in fallbacks.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var rel = $"gifs/random?api_key={Uri.EscapeDataString(key)}&tag={Uri.EscapeDataString(t)}&rating={Uri.EscapeDataString(rating)}&random_id={randomId}";
                var randomUrl = BuildAbsoluteUrl(rel);

                using var res = await _http.GetAsync(randomUrl, ct);
                if (!res.IsSuccessStatusCode) continue;

                var json = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    var dto = MapToDto(data);
                    if (dto != null) return dto;
                }
            }

            return null;
        }

        private Uri BuildAbsoluteUrl(string relativeOrAbsolute)
        {
            var baseUrl = _http.BaseAddress?.ToString() ?? "https://api.giphy.com/v1/";
            return Uri.TryCreate(relativeOrAbsolute, UriKind.Absolute, out var abs)
                ? abs
                : new Uri(new Uri(baseUrl), relativeOrAbsolute);
        }

        private static GiphyDto? MapToDto(JsonElement data)
        {
            string? title = data.TryGetProperty("title", out var t) ? t.GetString() : null;
            string? imgUrl = null;
            int w = 0, h = 0;

            if (data.TryGetProperty("images", out var images))
            {
                // Prefer webp (smaller), fallback to downsized gif, then original gif
                if (images.TryGetProperty("original", out var original) &&
                    original.TryGetProperty("webp", out var webpProp) &&
                    !string.IsNullOrWhiteSpace(webpProp.GetString()))
                {
                    imgUrl = webpProp.GetString();
                    w = int.TryParse(original.GetProperty("width").GetString(), out var ow) ? ow : 0;
                    h = int.TryParse(original.GetProperty("height").GetString(), out var oh) ? oh : 0;
                }
                if (imgUrl is null && images.TryGetProperty("downsized_medium", out var med) &&
                    med.TryGetProperty("url", out var gifProp))
                {
                    imgUrl = gifProp.GetString();
                    w = int.TryParse(med.GetProperty("width").GetString(), out var mw) ? mw : 0;
                    h = int.TryParse(med.GetProperty("height").GetString(), out var mh) ? mh : 0;
                }
                if (imgUrl is null && images.TryGetProperty("original", out var orig2) &&
                    orig2.TryGetProperty("url", out var origGif))
                {
                    imgUrl = origGif.GetString();
                    w = int.TryParse(orig2.GetProperty("width").GetString(), out var ow2) ? ow2 : 0;
                    h = int.TryParse(orig2.GetProperty("height").GetString(), out var oh2) ? oh2 : 0;
                }
            }

            if (string.IsNullOrWhiteSpace(imgUrl)) return null;
            return new GiphyDto(title ?? "Random GIF", imgUrl!, w, h);
        }
    }
}
