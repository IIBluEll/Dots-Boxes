using System;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Gameplay
{
    public enum LOCAL_GAME_RESULT_ENUM
    {
        NONE,
        WIN,
        LOSE,
        DRAW
    }

    public sealed class Result_Model
    {
        public LOCAL_GAME_RESULT_ENUM LocalGameResult { get; private set; } = LOCAL_GAME_RESULT_ENUM.NONE;
        public int LocalPlayerScore { get; private set; }
        public int OpponentScore { get; private set; }
        public bool HasResult => LocalGameResult != LOCAL_GAME_RESULT_ENUM.NONE;

        public void SetResult(
            GAME_RESULT_ENUM gameResult ,
            PLAYER_INDEX_ENUM localPlayerIndex ,
            int playerOneScore ,
            int playerTwoScore)
        {
            if ( gameResult == GAME_RESULT_ENUM.IN_PROGRESS )
            {
                throw new ArgumentException("진행 중인 게임은 Result 화면에 표시할 수 없습니다." , nameof(gameResult));
            }

            if ( localPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_ONE &&
                 localPlayerIndex != PLAYER_INDEX_ENUM.PLAYER_TWO )
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerIndex));
            }

            if ( playerOneScore < 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(playerOneScore));
            }

            if ( playerTwoScore < 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(playerTwoScore));
            }

            bool isLocalPlayerOne = localPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE;

            LocalPlayerScore = isLocalPlayerOne ? playerOneScore : playerTwoScore;
            OpponentScore = isLocalPlayerOne ? playerTwoScore : playerOneScore;
            LocalGameResult = GetLocalGameResult(gameResult , localPlayerIndex);
        }

        private static LOCAL_GAME_RESULT_ENUM GetLocalGameResult(
            GAME_RESULT_ENUM gameResult ,
            PLAYER_INDEX_ENUM localPlayerIndex)
        {
            switch ( gameResult )
            {
                case GAME_RESULT_ENUM.PLAYER_ONE_WIN:
                    return localPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                        ? LOCAL_GAME_RESULT_ENUM.WIN
                        : LOCAL_GAME_RESULT_ENUM.LOSE;

                case GAME_RESULT_ENUM.PLAYER_TWO_WIN:
                    return localPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_TWO
                        ? LOCAL_GAME_RESULT_ENUM.WIN
                        : LOCAL_GAME_RESULT_ENUM.LOSE;

                case GAME_RESULT_ENUM.DRAW:
                    return LOCAL_GAME_RESULT_ENUM.DRAW;

                default:
                    throw new ArgumentOutOfRangeException(nameof(gameResult) , gameResult , "표시할 수 없는 게임 결과입니다.");
            }
        }
    }
}
