using System;

namespace DotsAndBoxes.Shared
{
    public sealed class MatchAssignment
    {
        public Guid MatchId { get; set; }
        public PLAYER_INDEX_ENUM LocalPlayerIndex { get; set; } = PLAYER_INDEX_ENUM.NONE;
        public Guid OpponentUserId { get; set; }
    }
}
