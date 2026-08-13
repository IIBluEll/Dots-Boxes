using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class GameCompletionTests
    {
        private static readonly int[] DRAW_EDGE_SEQUENCE =
        {
            5, 35, 12, 19, 15, 10, 30, 21,
            8, 11, 39, 0, 28, 14, 38, 18,
            22, 31, 27, 24, 17, 32, 7, 26,
            37, 13, 2, 1, 33, 16, 29, 36,
            23, 6, 34, 4, 3, 25, 9, 20
        };

        [Test]
        public void NewBoard_ResultIsInProgress()
        {
            DotsBoard board = new DotsBoard();

            Assert.That(board.IsGameFinished , Is.False);

            Assert.That(
                board.GameResult ,
                Is.EqualTo(GAME_RESULT_ENUM.IN_PROGRESS));
        }

        [Test]
        public void CompleteGame_ResolvesAllEdgesAndBoxes()
        {
            DotsBoard board = new DotsBoard();
            MoveResult lastResult = null;

            for ( int i = 0;
                 i < DRAW_EDGE_SEQUENCE.Length;
                 i++ )
            {
                int edgeId = DRAW_EDGE_SEQUENCE[i];

                lastResult = ConfirmCurrentPlayer(
                    board ,
                    edgeId);

                Assert.That(lastResult.IsValid , Is.True);

                Assert.That(
                    board.ConfirmedEdgeCount ,
                    Is.EqualTo(i + 1));

                Assert.That(
                    board.OwnedBoxCount ,
                    Is.EqualTo(
                        board.PlayerOneScore +
                        board.PlayerTwoScore));
            }

            Assert.That(lastResult , Is.Not.Null);
            Assert.That(lastResult.IsGameFinished , Is.True);

            Assert.That(
                board.ConfirmedEdgeCount ,
                Is.EqualTo(BoardTopology.EDGE_COUNT));

            Assert.That(
                board.OwnedBoxCount ,
                Is.EqualTo(BoardTopology.BOX_COUNT));

            Assert.That(
                board.PlayerOneScore +
                board.PlayerTwoScore ,
                Is.EqualTo(BoardTopology.BOX_COUNT));
        }

        [Test]
        public void CompleteGame_CanFinishAsDraw()
        {
            DotsBoard board = CompleteDrawGame();

            Assert.That(board.IsGameFinished , Is.True);
            Assert.That(board.PlayerOneScore , Is.EqualTo(8));
            Assert.That(board.PlayerTwoScore , Is.EqualTo(8));

            Assert.That(
                board.GameResult ,
                Is.EqualTo(GAME_RESULT_ENUM.DRAW));
        }

        [Test]
        public void FinishedGame_RejectsAdditionalMove()
        {
            DotsBoard board = CompleteDrawGame();

            int confirmedEdgeCount =
                board.ConfirmedEdgeCount;

            int ownedBoxCount =
                board.OwnedBoxCount;

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                board.CurrentPlayerIndex,
                0);

            Assert.That(result.IsValid , Is.False);

            Assert.That(
                result.Error ,
                Is.EqualTo(
                    MOVE_ERROR_ENUM.GAME_ALREADY_FINISHED));

            Assert.That(result.IsGameFinished , Is.True);

            Assert.That(
                board.ConfirmedEdgeCount ,
                Is.EqualTo(confirmedEdgeCount));

            Assert.That(
                board.OwnedBoxCount ,
                Is.EqualTo(ownedBoxCount));
        }

        private static DotsBoard CompleteDrawGame()
        {
            DotsBoard board = new DotsBoard();

            for ( int i = 0;
                 i < DRAW_EDGE_SEQUENCE.Length;
                 i++ )
            {
                ConfirmCurrentPlayer(
                    board ,
                    DRAW_EDGE_SEQUENCE[ i ]);
            }

            return board;
        }

        private static MoveResult ConfirmCurrentPlayer(
            DotsBoard board ,
            int edgeId)
        {
            return DotsRule.TryConfirmEdge(
                board ,
                board.CurrentPlayerIndex ,
                edgeId);
        }
    }
}