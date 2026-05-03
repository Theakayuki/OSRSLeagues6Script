using PointsLeftChecker.Models;
using System.Text.Json;

namespace PointsLeftChecker.State;

public class JsonFileStateStore : IStateStore
{
    private readonly string _path;
    private readonly JsonSerializerOptions _options;

    public JsonFileStateStore(string path, JsonSerializerOptions options)
    {
        _path = path;
        _options = options;
    }

    public async Task<AppState?> LoadAsync()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            var content = await File.ReadAllTextAsync(_path);
            return JsonSerializer.Deserialize<AppState>(content, _options);
        }
        catch
        {
            return null;
        }
    }

    public async Task SaveAsync(AppState state)
    {
        var tmp = _path + ".tmp";
        var json = JsonSerializer.Serialize(state, _options);
        await File.WriteAllTextAsync(tmp, json);
        File.Move(tmp, _path, true);
    }
}
