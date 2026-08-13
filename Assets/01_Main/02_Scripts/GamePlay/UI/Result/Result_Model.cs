using System;
using DotsAndBoxes.Shared;

namespace DotsAndBoxes.Gameplay
{
    public sealed class Result_Model
    {
        public GAME_RESULT_ENUM GameResult { get; private set; } = GAME_RESULT_ENUM.IN_PROGRESS;
        public int PlayerOneScore { get; private set; }
        public int PlayerTwoScore { get; private set; }
        public bool HasResult => GameResult != GAME_RESULT_ENUM.IN_PROGRESS;

        public void SetResult(GAME_RESULT_ENUM gameResult , int playerOneScore , int playerTwoScore)
        {
            if ( gameResult == GAME_RESULT_ENUM.IN_PROGRESS )
            {
                throw new ArgumentException("진행 중인 게임은 Result 화면에 표시할 수 없습니다." , nameof(gameResult));
            }

            if ( playerOneScore < 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(playerOneScore));
            }

            if ( playerTwoScore < 0 )
            {
                throw new ArgumentOutOfRangeException(nameof(playerTwoScore));
            }

            GameResult = gameResult;
            PlayerOneScore = playerOneScore;
            PlayerTwoScore = playerTwoScore;
        }
    }
}