using PointsLeftChecker.Models;
using System.Text.Json;
using System.Net.Http.Json;

namespace PointsLeftChecker.Services;

public class PlayerService : IPlayerService
{
    private readonly HttpClient _client;

    public PlayerService(HttpClient client)
    {
        _client = client;
    }

    public async Task<PlayerData?> GetPlayerDataAsync(string username, JsonSerializerOptions options)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, $"https://sync.runescape.wiki/runelite/player/{username}/DEMONIC_PACTS_LEAGUE");
        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        req.Headers.Add("Origin", "https://oldschool.runescape.wiki");
        req.Headers.Add("Referer", "https://oldschool.runescape.wiki/");
        req.Headers.Add("sec-fetch-mode", "cors");
        req.Headers.Add("sec-fetch-site", "same-site");
        req.Headers.Add("sec-fetch-dest", "empty");
        req.Headers.Add("sec-ch-ua", "Microsoft");
        req.Headers.Add("sec-ch-ua-mobile", "?1");
        req.Headers.Add("sec-ch-ua-platform", "Android");
        req.Headers.Add("accept-language", "en-US,en;q=0.9");
        req.Headers.Add("pragma", "no-cache");
        req.Headers.Add("priority", "u=1, i");

        var response = await _client.SendAsync(req);
        if (!response.IsSuccessStatusCode)
            return null;

        try
        {
            var data = await response.Content.ReadFromJsonAsync<PlayerData>(options);
            return data;
        }
        catch
        {
            return null;
        }
    }
}
