
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

var assembly = Assembly.GetExecutingAssembly();
var leaguesTasksResourceName = assembly.GetManifestResourceNames().FirstOrDefault(name => name.EndsWith("leagues_tasks.json", StringComparison.OrdinalIgnoreCase));

string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PointsLeftChecker");
Directory.CreateDirectory(appDir);
string statePath = Path.Combine(appDir, "app_state.json");

var stateOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true,
    WriteIndented = true
};

AppState? state = null;
if (File.Exists(statePath))
{
    try
    {
        state = JsonSerializer.Deserialize<AppState>(File.ReadAllText(statePath), stateOptions);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to load application state: {ex.Message}");
    }
}

var leaguesTasksJson = string.Empty;
if (leaguesTasksResourceName != null)
{
    using var stream = assembly.GetManifestResourceStream(leaguesTasksResourceName);
    using var reader = new StreamReader(stream);
    leaguesTasksJson = reader.ReadToEnd();
}
else
{
    Console.WriteLine("Failed to load embedded resource 'leagues_tasks.json'. Please ensure the file is included in the project and marked as an embedded resource.");
    return;
}

string username = state?.LastUsername ?? string.Empty;
HttpClient client = new();
Console.WriteLine("Welcome to the OSRS Leagues 6 points left checker!");
Console.WriteLine($"Please enter your username(case-sensitive) or press Enter to use previous: {username}");

string input = Console.ReadLine();

if (!string.IsNullOrWhiteSpace(input))
{
    username = input.Trim();
}

if (string.IsNullOrEmpty(username))
{
    Console.WriteLine("No username provided. Exiting...");
    return;
}



HttpRequestMessage req = new(HttpMethod.Get, $"https://sync.runescape.wiki/runelite/player/{username}/DEMONIC_PACTS_LEAGUE");
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

HttpResponseMessage response = await client.SendAsync(req);
if (!response.IsSuccessStatusCode)
{
    Console.WriteLine($"Failed to fetch data for user '{username}'. Status code: {response.StatusCode}, Check if the username is correct and try again.");
    return;
}

JsonSerializerOptions options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};

var playerData = await response.Content.ReadFromJsonAsync<PlayerData>(options);
if (playerData == null)
{
    Console.WriteLine("Failed to parse player data. Please try again later.");
    return;
}

var leaguesTables = JsonSerializer.Deserialize<List<LeaguesTable>>(leaguesTasksJson, options);

if (leaguesTables == null)
{
    Console.WriteLine("Failed to load leagues tasks data. Please ensure 'leagues_tasks.json' is present and correctly formatted.");
    return;
}

int totalPoints = 0;
int totalPointsLeft = 0;

foreach (var league in leaguesTables)
{
    totalPoints += int.TryParse(league.Points, out int points) ? points : 0;
    if (playerData.LeagueTasks.Contains(int.TryParse(league.Id, out int taskId) ? taskId : -1))
    {
        totalPointsLeft += int.TryParse(league.Points, out int leftPoints) ? leftPoints : 0;
    }
}

var newState = new AppState(username, DateTime.UtcNow, playerData);
string temp = statePath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(newState, stateOptions));
File.Move(temp, statePath, true);

Console.WriteLine($"Total points available: {totalPoints}");
Console.WriteLine($"Points left to complete: {totalPointsLeft}");

public record LeaguesTable(string Id, string Area, string Name, string Task, string Requirements, string Points, string Completion);
public record PlayerData(string Username, DateTime Timestamp, IReadOnlyList<int> LeagueTasks);

public record AppState(string? LastUsername, DateTime LastRun, PlayerData? CachedData);