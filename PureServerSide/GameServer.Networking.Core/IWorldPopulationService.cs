namespace GameServer.Networking.Core;

public interface IWorldPopulationService
{
    Task EnsureWorldPopulatedAsync(string worldId, CancellationToken ct = default);
    void Tick(float deltaTime);
}
