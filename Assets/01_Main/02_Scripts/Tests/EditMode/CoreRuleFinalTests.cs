using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class CoreRuleFinalTests
    {
        private static readonly int[]
            PLAYER_ONE_WIN_EDGE_SEQUENCE =
        {
            23, 2, 11, 1, 37, 32, 5, 9,
            17, 29, 34, 20, 38, 21, 27, 22,
            10, 33, 18, 35, 39, 14, 26, 19,
            13, 12, 0, 15, 3, 6, 25, 24,
            30, 28, 31, 7, 16, 4, 36, 8
        };

        [Test]
        public void CompleteGame_PlayerOneCanWin()
        {
            DotsBoard board = ReplayGame(
                PLAYER_INDEX_ENUM.PLAYER_ONE);

            Assert.That(board.IsGameFinished , Is.True);
            Assert.That(board.PlayerOneScore , Is.EqualTo(9));
            Assert.That(board.PlayerTwoScore , Is.EqualTo(7));

            Assert.That(
                board.GameResult ,
                Is.EqualTo(
                    GAME_RESULT_ENUM.PLAYER_ONE_WIN));
        }

        [Test]
        public void CompleteGame_PlayerTwoCanWin()
        {
            DotsBoard board = ReplayGame(
                PLAYER_INDEX_ENUM.PLAYER_TWO);

            Assert.That(board.IsGameFinished , Is.True);
            Assert.That(board.PlayerOneScore , Is.EqualTo(7));
            Assert.That(board.PlayerTwoScore , Is.EqualTo(9));

            Assert.That(
                board.GameResult ,
                Is.EqualTo(
                    GAME_RESULT_ENUM.PLAYER_TWO_WIN));
        }

        [Test]
        public void ReplaySameSequence_ProducesSameState()
        {
            DotsBoard firstBoard = ReplayGame(
                PLAYER_INDEX_ENUM.PLAYER_ONE);

            DotsBoard secondBoard = ReplayGame(
                PLAYER_INDEX_ENUM.PLAYER_ONE);

            Assert.That(
                firstBoard.CurrentPlayerIndex ,
                Is.EqualTo(
                    secondBoard.CurrentPlayerIndex));

            Assert.That(
                firstBoard.PlayerOneScore ,
                Is.EqualTo(secondBoard.PlayerOneScore));

            Assert.That(
                firstBoard.PlayerTwoScore ,
                Is.EqualTo(secondBoard.PlayerTwoScore));

            Assert.That(
                firstBoard.GameResult ,
                Is.EqualTo(secondBoard.GameResult));

            AssertEdgeStatesAreEqual(
                firstBoard ,
                secondBoard);

            AssertBoxStatesAreEqual(
                firstBoard ,
                secondBoard);
        }

        [Test]
        public void ReplayGame_StateCountsNeverDecrease()
        {
            DotsBoard board = new DotsBoard();

            int previousConfirmedEdgeCount = 0;
            int previousOwnedBoxCount = 0;

            for ( int i = 0;
                 i < PLAYER_ONE_WIN_EDGE_SEQUENCE.Length;
                 i++ )
            {
                MoveResult result =
                    ConfirmCurrentPlayer(
                        board,
                        PLAYER_ONE_WIN_EDGE_SEQUENCE[i]);

                Assert.That(result.IsValid , Is.True);

                Assert.That(
                    board.ConfirmedEdgeCount ,
                    Is.GreaterThanOrEqualTo(
                        previousConfirmedEdgeCount));

                Assert.That(
                    board.OwnedBoxCount ,
                    Is.GreaterThanOrEqualTo(
                        previousOwnedBoxCount));

                Assert.That(
                    board.OwnedBoxCount ,
                    Is.EqualTo(
                        board.PlayerOneScore +
                        board.PlayerTwoScore));

                previousConfirmedEdgeCount =
                    board.ConfirmedEdgeCount;

                previousOwnedBoxCount =
                    board.OwnedBoxCount;
            }

            Assert.That(
                board.ConfirmedEdgeCount ,
                Is.EqualTo(BoardTopology.EDGE_COUNT));

            Assert.That(
                board.OwnedBoxCount ,
                Is.EqualTo(BoardTopology.BOX_COUNT));
        }

        [Test]
        public void ConfirmEdge_InvalidPlayerDoesNotChangeBoard()
        {
            DotsBoard board = new DotsBoard();

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.NONE,
                0);

            Assert.That(result.IsValid , Is.False);

            Assert.That(
                result.Error ,
                Is.EqualTo(MOVE_ERROR_ENUM.INVALID_PLAYER));

            Assert.That(board.ConfirmedEdgeCount , Is.Zero);
            Assert.That(board.OwnedBoxCount , Is.Zero);

            Assert.That(
                board.CurrentPlayerIndex ,
                Is.EqualTo(
                    PLAYER_INDEX_ENUM.PLAYER_ONE));
        }

        private static DotsBoard ReplayGame(
            PLAYER_INDEX_ENUM startingPlayerIndex)
        {
            DotsBoard board =
                new DotsBoard(startingPlayerIndex);

            for ( int i = 0;
                 i < PLAYER_ONE_WIN_EDGE_SEQUENCE.Length;
                 i++ )
            {
                MoveResult result =
                    ConfirmCurrentPlayer(
                        board,
                        PLAYER_ONE_WIN_EDGE_SEQUENCE[i]);

                Assert.That(
                    result.IsValid ,
                    Is.True ,
                    $"Edge {PLAYER_ONE_WIN_EDGE_SEQUENCE[ i ]} 처리 실패");
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

        private static void AssertEdgeStatesAreEqual(
            DotsBoard firstBoard ,
            DotsBoard secondBoard)
        {
            for ( int edgeId = 0;
                 edgeId < BoardTopology.EDGE_COUNT;
                 edgeId++ )
            {
                Assert.That(
                    firstBoard
                        .GetEdge(edgeId)
                        .OwnerPlayerIndex ,
                    Is.EqualTo(
                        secondBoard
                            .GetEdge(edgeId)
                            .OwnerPlayerIndex) ,
                    $"Edge {edgeId}의 소유자가 다릅니다.");
            }
        }

        private static void AssertBoxStatesAreEqual(
            DotsBoard firstBoard ,
            DotsBoard secondBoard)
        {
            for ( int boxId = 0;
                 boxId < BoardTopology.BOX_COUNT;
                 boxId++ )
            {
                Assert.That(
                    firstBoard
                        .GetBox(boxId)
                        .OwnerPlayerIndex ,
                    Is.EqualTo(
                        secondBoard
                            .GetBox(boxId)
                            .OwnerPlayerIndex) ,
                    $"Box {boxId}의 소유자가 다릅니다.");
            }
        }
    }
}