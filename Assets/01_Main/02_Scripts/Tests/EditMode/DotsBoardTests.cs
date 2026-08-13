using System;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class DotsBoardTests
    {
        [Test]
        public void NewBoard_CreatesAllEdgesAndBoxes()
        {
            DotsBoard board = new DotsBoard();

            Assert.That(
                board.Edges.Count ,
                Is.EqualTo(BoardTopology.EDGE_COUNT));

            Assert.That(
                board.Boxes.Count ,
                Is.EqualTo(BoardTopology.BOX_COUNT));
        }

        [Test]
        public void NewBoard_AssignsSequentialEdgeIDs()
        {
            DotsBoard board = new DotsBoard();

            for ( int edgeId = 0;
                 edgeId < BoardTopology.EDGE_COUNT;
                 edgeId++ )
            {
                Assert.That(
                    board.Edges[ edgeId ].EdgeId ,
                    Is.EqualTo(edgeId));
            }
        }

        [Test]
        public void NewBoard_AssignsSequentialBoxIDs()
        {
            DotsBoard board = new DotsBoard();

            for ( int boxId = 0;
                 boxId < BoardTopology.BOX_COUNT;
                 boxId++ )
            {
                Assert.That(
                    board.Boxes[ boxId ].BoxId ,
                    Is.EqualTo(boxId));
            }
        }

        [Test]
        public void NewBoard_HasNoConfirmedEdgesOrOwnedBoxes()
        {
            DotsBoard board = new DotsBoard();

            Assert.That(board.ConfirmedEdgeCount , Is.Zero);
            Assert.That(board.OwnedBoxCount , Is.Zero);
            Assert.That(board.PlayerOneScore , Is.Zero);
            Assert.That(board.PlayerTwoScore , Is.Zero);
            Assert.That(board.IsGameFinished , Is.False);
        }

        [TestCase(PLAYER_INDEX_ENUM.PLAYER_ONE)]
        [TestCase(PLAYER_INDEX_ENUM.PLAYER_TWO)]
        public void NewBoard_UsesSpecifiedStartingPlayer(
            PLAYER_INDEX_ENUM startingPlayerIndex)
        {
            DotsBoard board =
                new DotsBoard(startingPlayerIndex);

            Assert.That(
                board.CurrentPlayerIndex ,
                Is.EqualTo(startingPlayerIndex));
        }

        [Test]
        public void NewBoard_InvalidStartingPlayer_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DotsBoard(PLAYER_INDEX_ENUM.NONE));
        }

        [TestCase(-1)]
        [TestCase(40)]
        public void GetEdge_InvalidID_ThrowsException(int edgeId)
        {
            DotsBoard board = new DotsBoard();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => board.GetEdge(edgeId));
        }

        [TestCase(-1)]
        [TestCase(16)]
        public void GetBox_InvalidID_ThrowsException(int boxId)
        {
            DotsBoard board = new DotsBoard();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => board.GetBox(boxId));
        }

        [Test]
        public void GetScore_InvalidPlayer_ThrowsException()
        {
            DotsBoard board = new DotsBoard();

            Assert.Throws<ArgumentOutOfRangeException>(
                () => board.GetScore(PLAYER_INDEX_ENUM.NONE));
        }
    }
}