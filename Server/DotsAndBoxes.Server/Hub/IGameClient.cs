using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Hubs
{
    public interface IGameClient
    {
        Task MatchStateChanged(MatchSnapshot snapshot);
    }
}