namespace DdosDefendSystem.Agent.Services;

public interface IConnectionSnapshotProvider
{
    Task<IReadOnlyDictionary<string, int>> GetConnectionCountsAsync(CancellationToken cancellationToken = default);
}
