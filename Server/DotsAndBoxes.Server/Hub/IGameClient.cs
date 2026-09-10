using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Hubs
{
    public interface IGameClient
    {
        Task MatchFound(MatchAssignment assignment);
        Task MatchStateChanged(MatchSnapshot snapshot);
        Task OpponentPreviewChanged(OpponentPreviewUpdate update);
    }
}
