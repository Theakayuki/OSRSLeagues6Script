namespace PointsLeftChecker.Models;

public record AppState(string? LastUsername, DateTime LastRun, PlayerData? CachedData);
