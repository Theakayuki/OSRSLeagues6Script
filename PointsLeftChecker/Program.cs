
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

string input = Console.ReadLine() ?? string.Empty;

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

Console.WriteLine("Enter the number corresponding locations you have unlocked, separated by commas. For example, '1,3,5' for General, Karamja, and Desert.");
Console.WriteLine($"Or if you have entered locations before and not changed, just press Enter to use previous locations: {(state?.PreviousLocations?.Count > 0 ? string.Join(", ", state.PreviousLocations) : "None")}");
Console.WriteLine("Available locations:");
foreach (var location in Enum.GetValues<Locations>())
{
    Console.WriteLine($"{(int)location}. {location}");
}

string locationsInput = Console.ReadLine() ?? string.Empty;
List<Locations> selectedLocations = new();
if (string.IsNullOrWhiteSpace(locationsInput) && state?.PreviousLocations != null)
{
    selectedLocations = state.PreviousLocations;
}
else
{
    var locationIds = locationsInput.Split(',').Select(id => id.Trim());
    foreach (var id in locationIds)
    {
        if (int.TryParse(id, out int locationId) && Enum.IsDefined(typeof(Locations), locationId))
        {
            selectedLocations.Add((Locations)locationId);
        }
        else
        {
            Console.WriteLine($"Invalid location ID: {id}. Skipping.");
        }
    }
}

Console.WriteLine("Any keywords to filter tasks by? For example, entering 'kill' will only show tasks with 'kill' in the description. Separate multiple keywords with commas. Or just press Enter to skip.");
string keywordsInput = Console.ReadLine() ?? string.Empty;
List<string> keywords = keywordsInput.Split(',').Select(k => k.Trim()).Where(k => !string.IsNullOrEmpty(k)).ToList();

int totalPoints = 0;
int totalPointsLeft = 0;

foreach (var league in leaguesTables)
{
    if (selectedLocations.Contains(Enum.Parse<Locations>(league.Area)))
    {
        totalPoints += int.TryParse(league.Points, out int points) ? points : 0;
        if (!playerData.LeagueTasks.Contains(int.TryParse(league.Id, out int taskId) ? taskId : -1))
        {
            if (keywords.Count == 0 || keywords.Any(k => league.Task.Contains(k, StringComparison.OrdinalIgnoreCase) || league.Requirements.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                totalPointsLeft += int.TryParse(league.Points, out int leftPoints) ? leftPoints : 0;
                Console.WriteLine($"Task: {league.Task}");
                Console.WriteLine($"Area: {league.Area}");
                Console.WriteLine($"Points: {league.Points}");
                Console.WriteLine($"Requirements: {league.Requirements}");
                Console.WriteLine();
            }
        }
    }
}

var newState = new AppState(username, DateTime.UtcNow, playerData, selectedLocations);
string temp = statePath + ".tmp";
File.WriteAllText(temp, JsonSerializer.Serialize(newState, stateOptions));
File.Move(temp, statePath, true);

Console.WriteLine($"Total points available: {totalPoints}");
Console.WriteLine($"Points left to complete: {totalPointsLeft}");

public record LeaguesTable(string Id, string Area, string Name, string Task, string Requirements, string Points, string Completion);
/// <summary>
/// Represents the player data fetched from the RuneLite API, including username, timestamp of data retrieval, and a list of completed league task IDs.
/// </summary>
/// <param name="Username">The username of the player.</param>
/// <param name="Timestamp">The timestamp when the data was retrieved.</param>
/// <param name="LeagueTasks">A list of completed league task IDs.</param>
public record PlayerData(string Username, DateTime Timestamp, IReadOnlyList<int> LeagueTasks);
public record AppState(string? LastUsername, DateTime LastRun, PlayerData? CachedData, List<Locations> PreviousLocations);

// String enum for locations to covert numbers user entered numbers into readable locations
public enum Locations
{
    General = 1,
    Varlamore = 2,
    Karamja = 3,
    Asgarnia = 4,
    Desert = 5,
    Fremennik = 6,
    Kandarin = 7,
    Kourend = 8,
    Morytania = 9,
    Tirannwn = 10,
    Wilderness = 11,
}