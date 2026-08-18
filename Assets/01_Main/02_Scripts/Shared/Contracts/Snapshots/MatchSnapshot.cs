using System;

namespace DotsAndBoxes.Shared
{
    public sealed class MatchSnapshot
    {
        public const int CURRENT_SCHEMA_VERSION = 1;

        public int SchemaVersion { get; set; } = CURRENT_SCHEMA_VERSION;
        public Guid MatchId { get; set; }
        public long Revision { get; set; }
        public SERVER_MATCH_STATE_ENUM MatchState { get; set; } = SERVER_MATCH_STATE_ENUM.NONE;

        public Guid PlayerOneUserId { get; set; }
        public Guid PlayerTwoUserId { get; set; }
        public PLAYER_INDEX_ENUM CurrentPlayerIndex { get; set; } = PLAYER_INDEX_ENUM.NONE;

        public PLAYER_INDEX_ENUM[] EdgeOwners { get; set; } = Array.Empty<PLAYER_INDEX_ENUM>();
        public PLAYER_INDEX_ENUM[] BoxOwners { get; set; } = Array.Empty<PLAYER_INDEX_ENUM>();

        public int PlayerOneScore { get; set; }
        public int PlayerTwoScore { get; set; }

        public GAME_RESULT_ENUM GameResult { get; set; } = GAME_RESULT_ENUM.IN_PROGRESS;
    }
}
