using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Server.Matches
{
    public class MatchRoom
    {
        private readonly DotsBoard BOARD;

        private GAME_RESULT_ENUM _gameResult;
        private MATCH_FINISH_REASON_ENUM _finishReason;

        public Guid MatchId { get; }
        public MatchPlayer PlayerOne { get; }
        public MatchPlayer PlayerTwo { get; }

        public long Revision { get; private set; }
        public SERVER_MATCH_STATE_ENUM MatchState { get; private set; }
        public DateTimeOffset? TurnDeadlineUtc { get; private set; }

        public PLAYER_INDEX_ENUM CurrentPlayerIndex => BOARD.CurrentPlayerIndex;
        public GAME_RESULT_ENUM GameResult => _gameResult;
        public MATCH_FINISH_REASON_ENUM FinishReason => _finishReason;

        public MatchRoom(Guid matchId , MatchPlayer playerOne , MatchPlayer playerTwo , PLAYER_INDEX_ENUM startingPlayerIndex , DateTimeOffset turnDeadLine)
        {
            if ( matchId == Guid.Empty )
            {
                throw new ArgumentException("MatchId는 Guid.Empty일 수 없습니다." , nameof(matchId));
            }

            PlayerOne = playerOne ?? throw new ArgumentNullException(nameof(playerOne));
            PlayerTwo = playerTwo ?? throw new ArgumentNullException(nameof(playerTwo));

            if ( playerOne.UserId == playerTwo.UserId )
            {
                throw new ArgumentException("같은 사용자를 두 Player Slot에 배치할 수 없습니다.");
            }

            if ( startingPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE && startingPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO )
            {
                throw new ArgumentOutOfRangeException(nameof(startingPlayerIndex));
            }

            MatchId = matchId;
            BOARD = new DotsBoard(startingPlayerIndex);

            Revision = 0;
            MatchState = SERVER_MATCH_STATE_ENUM.ACTIVE;
            TurnDeadlineUtc = turnDeadLine.ToUniversalTime();

            _gameResult = GAME_RESULT_ENUM.IN_PROGRESS;
            _finishReason = MATCH_FINISH_REASON_ENUM.NONE;
        }

        public bool TryGetPlayerIndex(Guid userId , out PLAYER_INDEX_ENUM playerIndex)
        {
            if ( PlayerOne.UserId == userId )
            {
                playerIndex = PLAYER_INDEX_ENUM.PLAYER_ONE;
                return true;
            }

            if ( PlayerTwo.UserId == userId )
            {
                playerIndex = PLAYER_INDEX_ENUM.PLAYER_TWO;
                return true;
            }

            playerIndex = PLAYER_INDEX_ENUM.NONE;
            return false;
        }

        public MatchSnapshot CreateSnapshot()
        {
            PLAYER_INDEX_ENUM[] edgeOwners = new PLAYER_INDEX_ENUM[BoardTopology.EDGE_COUNT];
            PLAYER_INDEX_ENUM[] boxOwners = new PLAYER_INDEX_ENUM[BoardTopology.BOX_COUNT];

            for ( int edgeId = 0; edgeId < edgeOwners.Length; edgeId++ )
            {
                edgeOwners[ edgeId ] = BOARD.GetEdge(edgeId).OwnerPlayerIndex;
            }

            for ( int boxId = 0; boxId < boxOwners.Length; boxId++ )
            {
                boxOwners[ boxId ] = BOARD.GetBox(boxId).OwnerPlayerIndex;
            }

            return new MatchSnapshot
            {
                MatchId = MatchId ,
                Revision = Revision ,
                MatchState = MatchState ,

                PlayerOneUserId = PlayerOne.UserId ,
                PlayerTwoUserId = PlayerTwo.UserId ,
                CurrentPlayerIndex = BOARD.CurrentPlayerIndex ,

                EdgeOwners = edgeOwners ,
                BoxOwners = boxOwners ,

                PlayerOneScore = BOARD.PlayerOneScore ,
                PlayerTwoScore = BOARD.PlayerTwoScore ,

                TurnDeadlineUtc = TurnDeadlineUtc ,
                GameResult = _gameResult ,
                FinishReason = _finishReason
            };
        }
    }
}
