using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class DotsRuleTests
    {
        [Test]
        public void ConfirmEdge_ValidMoveConfirmsEdge()
        {
            DotsBoard board = new DotsBoard();

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.PLAYER_ONE,
                0);

            Assert.That(result.IsValid , Is.True);
            Assert.That(result.Error , Is.EqualTo(MOVE_ERROR_ENUM.NONE));
            Assert.That(result.EdgeId , Is.EqualTo(0));

            Assert.That(board.GetEdge(0).IsConfirmed , Is.True);
            Assert.That(
                board.GetEdge(0).OwnerPlayerIndex ,
                Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));

            Assert.That(board.ConfirmedEdgeCount , Is.EqualTo(1));
        }

        [Test]
        public void ConfirmEdge_NoCompletedBoxSwitchesTurn()
        {
            DotsBoard board = new DotsBoard();

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.PLAYER_ONE,
                0);

            Assert.That(result.HasExtraTurn , Is.False);
            Assert.That(result.CompletedBoxIds.Count , Is.Zero);

            Assert.That(
                board.CurrentPlayerIndex ,
                Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
        }

        [Test]
        public void ConfirmEdge_WrongPlayerIsRejected()
        {
            DotsBoard board = new DotsBoard();

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.PLAYER_TWO,
                0);

            Assert.That(result.IsValid , Is.False);

            Assert.That(
                result.Error ,
                Is.EqualTo(MOVE_ERROR_ENUM.NOT_YOUR_TURN));

            Assert.That(board.ConfirmedEdgeCount , Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(40)]
        public void ConfirmEdge_InvalidEdgeIsRejected(int edgeId)
        {
            DotsBoard board = new DotsBoard();

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.PLAYER_ONE,
                edgeId);

            Assert.That(result.IsValid , Is.False);

            Assert.That(
                result.Error ,
                Is.EqualTo(MOVE_ERROR_ENUM.INVALID_EDGE));

            Assert.That(board.ConfirmedEdgeCount , Is.Zero);
        }

        [Test]
        public void ConfirmEdge_AlreadyConfirmedEdgeIsRejected()
        {
            DotsBoard board = new DotsBoard();

            DotsRule.TryConfirmEdge(
                board ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                0);

            MoveResult result = DotsRule.TryConfirmEdge(
                board,
                PLAYER_INDEX_ENUM.PLAYER_TWO,
                0);

            Assert.That(result.IsValid , Is.False);

            Assert.That(
                result.Error ,
                Is.EqualTo(
                    MOVE_ERROR_ENUM.EDGE_ALREADY_CONFIRMED));

            Assert.That(board.ConfirmedEdgeCount , Is.EqualTo(1));
        }

        [Test]
        public void ConfirmEdge_CompletesBoxAndKeepsTurn()
        {
            DotsBoard board = new DotsBoard();

            ConfirmCurrentPlayer(board , 0);
            ConfirmCurrentPlayer(board , 4);
            ConfirmCurrentPlayer(board , 20);

            PLAYER_INDEX_ENUM completingPlayer =
                board.CurrentPlayerIndex;

            MoveResult result =
                ConfirmCurrentPlayer(board, 21);

            Assert.That(result.IsValid , Is.True);
            Assert.That(result.HasExtraTurn , Is.True);
            Assert.That(result.CompletedBoxIds.Count , Is.EqualTo(1));
            Assert.That(result.CompletedBoxIds[ 0 ] , Is.EqualTo(0));

            Assert.That(board.GetBox(0).IsOwned , Is.True);

            Assert.That(
                board.GetBox(0).OwnerPlayerIndex ,
                Is.EqualTo(completingPlayer));

            Assert.That(
                board.CurrentPlayerIndex ,
                Is.EqualTo(completingPlayer));

            Assert.That(
                board.GetScore(completingPlayer) ,
                Is.EqualTo(1));
        }

        [Test]
        public void ConfirmEdge_CanCompleteTwoBoxes()
        {
            DotsBoard board = new DotsBoard();

            ConfirmCurrentPlayer(board , 0);
            ConfirmCurrentPlayer(board , 4);
            ConfirmCurrentPlayer(board , 20);

            ConfirmCurrentPlayer(board , 1);
            ConfirmCurrentPlayer(board , 5);
            ConfirmCurrentPlayer(board , 22);

            PLAYER_INDEX_ENUM completingPlayer =
                board.CurrentPlayerIndex;

            MoveResult result =
                ConfirmCurrentPlayer(board, 21);

            Assert.That(result.IsValid , Is.True);
            Assert.That(result.HasExtraTurn , Is.True);
            Assert.That(result.CompletedBoxIds.Count , Is.EqualTo(2));

            Assert.That(
                result.CompletedBoxIds ,
                Does.Contain(0));

            Assert.That(
                result.CompletedBoxIds ,
                Does.Contain(1));

            Assert.That(
                board.GetScore(completingPlayer) ,
                Is.EqualTo(2));

            Assert.That(
                board.CurrentPlayerIndex ,
                Is.EqualTo(completingPlayer));
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