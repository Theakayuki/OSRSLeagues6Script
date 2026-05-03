using PointsLeftChecker.Models;

namespace PointsLeftChecker.State;

public interface IStateStore
{
    Task<AppState?> LoadAsync();
    Task SaveAsync(AppState state);
}
