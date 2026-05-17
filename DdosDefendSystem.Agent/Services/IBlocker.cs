namespace DdosDefendSystem.Agent.Services
{
    public interface IBlocker
    {
        Task BlockIpAsync(string ipAddress, TimeSpan duration);
        Task UnblockIpAsync(string ipAddress);
    }
}