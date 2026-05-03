using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using PointsLeftChecker.Models;
using PointsLeftChecker.Services;
using PointsLeftChecker.State;

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

var stateStore = new JsonFileStateStore(statePath, stateOptions);
AppState? state = await stateStore.LoadAsync();

var leaguesTasksJson = string.Empty;
if (leaguesTasksResourceName != null)
{
    using var stream = assembly.GetManifestResourceStream(leaguesTasksResourceName)!;
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
var playerService = new PlayerService(client);

Console.WriteLine("Welcome to the OSRS Leagues 6 points left checker!");
Console.WriteLine($"Please enter your username (case-sensitive) or press Enter to use previous: {username}");

var input = Console.ReadLine();
if (!string.IsNullOrWhiteSpace(input))
{
    username = input.Trim();
}

if (string.IsNullOrEmpty(username))
{
    Console.WriteLine("No username provided. Exiting...");
    return;
}

JsonSerializerOptions options = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
};

var playerData = await playerService.GetPlayerDataAsync(username, options);
if (playerData == null)
{
    // Fallback to cached data if available and recent
    if (state?.CachedData != null && (DateTime.UtcNow - state.LastRun) < TimeSpan.FromMinutes(30))
    {
        Console.WriteLine("Using cached player data from previous run.");
        playerData = state.CachedData;
    }
    else
    {
        Console.WriteLine("Failed to fetch or parse player data. Please try again later.");
        return;
    }
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
try
{
    await stateStore.SaveAsync(newState);
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: failed to save state: {ex.Message}");
}

Console.WriteLine($"Total points available: {totalPoints}");
Console.WriteLine($"Points left to complete: {totalPointsLeft}");
