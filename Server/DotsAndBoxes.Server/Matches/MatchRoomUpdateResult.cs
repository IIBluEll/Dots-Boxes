using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    public sealed class MatchRoomUpdateResult
    {
        public bool HasStateChanged { get; }
        public MatchSnapshot Snapshot { get; }

        public MatchRoomUpdateResult(
            bool hasStateChanged ,
            MatchSnapshot snapshot)
        {
            HasStateChanged = hasStateChanged;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
