using PointsLeftChecker.Models;
using System.Text.Json;

namespace PointsLeftChecker.Services;

public interface IPlayerService
{
    Task<PlayerData?> GetPlayerDataAsync(string username, JsonSerializerOptions options);
}
