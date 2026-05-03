namespace PointsLeftChecker.Models;

public record PlayerData(string Username, DateTime Timestamp, IReadOnlyList<int> LeagueTasks);
