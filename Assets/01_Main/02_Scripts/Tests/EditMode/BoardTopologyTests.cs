using System;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class BoardTopologyTests
    {
        [Test]
        public void BoardConstants_HaveExpectedCounts()
        {
            Assert.That(BoardTopology.DOT_ROWS , Is.EqualTo(5));
            Assert.That(BoardTopology.DOT_COLUMNS , Is.EqualTo(5));

            Assert.That(
                BoardTopology.HORIZONTAL_EDGE_COUNT ,
                Is.EqualTo(20));

            Assert.That(
                BoardTopology.VERTICAL_EDGE_COUNT ,
                Is.EqualTo(20));

            Assert.That(BoardTopology.EDGE_COUNT , Is.EqualTo(40));
            Assert.That(BoardTopology.BOX_COUNT , Is.EqualTo(16));
        }

        [TestCase(0 , 0 , 0)]
        [TestCase(0 , 3 , 3)]
        [TestCase(1 , 0 , 4)]
        [TestCase(4 , 3 , 19)]
        public void GetHorizontalEdgeID_ReturnsExpectedID(
            int row ,
            int column ,
            int expectedEdgeId)
        {
            int edgeId =
                BoardTopology.GetHorizontalEdgeID(row, column);

            Assert.That(edgeId , Is.EqualTo(expectedEdgeId));
        }

        [TestCase(0 , 0 , 20)]
        [TestCase(0 , 4 , 24)]
        [TestCase(1 , 0 , 25)]
        [TestCase(3 , 4 , 39)]
        public void GetVerticalEdgeID_ReturnsExpectedID(
            int row ,
            int column ,
            int expectedEdgeId)
        {
            int edgeId =
                BoardTopology.GetVerticalEdgeID(row, column);

            Assert.That(edgeId , Is.EqualTo(expectedEdgeId));
        }

        [TestCase(0 , 0 , 0)]
        [TestCase(0 , 3 , 3)]
        [TestCase(1 , 0 , 4)]
        [TestCase(3 , 3 , 15)]
        public void GetBoxID_ReturnsExpectedID(
            int row ,
            int column ,
            int expectedBoxId)
        {
            int boxId = BoardTopology.GetBoxID(row, column);

            Assert.That(boxId , Is.EqualTo(expectedBoxId));
        }

        [Test]
        public void GetBoxEdgeIDs_FirstBox_ReturnsExpectedEdges()
        {
            BoxEdgeIds edgeIds = BoardTopology.GetBoxEdgeIDs(0);

            Assert.That(edgeIds.TopEdgeId , Is.EqualTo(0));
            Assert.That(edgeIds.BottomEdgeId , Is.EqualTo(4));
            Assert.That(edgeIds.LeftEdgeId , Is.EqualTo(20));
            Assert.That(edgeIds.RightEdgeId , Is.EqualTo(21));
        }

        [Test]
        public void GetBoxEdgeIDs_LastBox_ReturnsExpectedEdges()
        {
            BoxEdgeIds edgeIds = BoardTopology.GetBoxEdgeIDs(15);

            Assert.That(edgeIds.TopEdgeId , Is.EqualTo(15));
            Assert.That(edgeIds.BottomEdgeId , Is.EqualTo(19));
            Assert.That(edgeIds.LeftEdgeId , Is.EqualTo(38));
            Assert.That(edgeIds.RightEdgeId , Is.EqualTo(39));
        }

        [Test]
        public void AdjacentBoxes_ShareSameVerticalEdge()
        {
            BoxEdgeIds leftBox = BoardTopology.GetBoxEdgeIDs(0);
            BoxEdgeIds rightBox = BoardTopology.GetBoxEdgeIDs(1);

            Assert.That(
                leftBox.RightEdgeId ,
                Is.EqualTo(rightBox.LeftEdgeId));
        }

        [Test]
        public void AdjacentBoxes_ShareSameHorizontalEdge()
        {
            BoxEdgeIds upperBox = BoardTopology.GetBoxEdgeIDs(0);
            BoxEdgeIds lowerBox = BoardTopology.GetBoxEdgeIDs(4);

            Assert.That(
                upperBox.BottomEdgeId ,
                Is.EqualTo(lowerBox.TopEdgeId));
        }

        [Test]
        public void InvalidHorizontalPosition_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BoardTopology.GetHorizontalEdgeID(5 , 0));
        }

        [Test]
        public void InvalidVerticalPosition_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BoardTopology.GetVerticalEdgeID(0 , 5));
        }

        [Test]
        public void InvalidBoxID_ThrowsException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BoardTopology.GetBoxEdgeIDs(16));
        }

        [Test]
        public void TopBoundaryEdge_HasOneAdjacentBox()
        {
            AdjacentBoxIDs boxIds =
        BoardTopology.GetAdjacentBoxIDs(0);

            Assert.That(boxIds.Count , Is.EqualTo(1));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(0));
            Assert.That(boxIds.SecondBoxId , Is.EqualTo(-1));
        }

        [Test]
        public void MiddleHorizontalEdge_HasTwoAdjacentBoxes()
        {
            AdjacentBoxIDs boxIds =
        BoardTopology.GetAdjacentBoxIDs(4);

            Assert.That(boxIds.Count , Is.EqualTo(2));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(0));
            Assert.That(boxIds.SecondBoxId , Is.EqualTo(4));
        }

        [Test]
        public void BottomBoundaryEdge_HasOneAdjacentBox()
        {
            AdjacentBoxIDs boxIds =
        BoardTopology.GetAdjacentBoxIDs(19);

            Assert.That(boxIds.Count , Is.EqualTo(1));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(15));
        }

        [Test]
        public void LeftBoundaryEdge_HasOneAdjacentBox()
        {
            AdjacentBoxIDs boxIds =
        BoardTopology.GetAdjacentBoxIDs(20);

            Assert.That(boxIds.Count , Is.EqualTo(1));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(0));
        }

        [Test]
        public void MiddleVerticalEdge_HasTwoAdjacentBoxes()
        {
            AdjacentBoxIDs boxIds =
        BoardTopology.GetAdjacentBoxIDs(21);

            Assert.That(boxIds.Count , Is.EqualTo(2));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(0));
            Assert.That(boxIds.SecondBoxId , Is.EqualTo(1));
        }

        [Test]
        public void RightBoundaryEdge_HasOneAdjacentBox()
        {
            AdjacentBoxIDs boxIds =
            BoardTopology.GetAdjacentBoxIDs(39);

            Assert.That(boxIds.Count , Is.EqualTo(1));
            Assert.That(boxIds.FirstBoxId , Is.EqualTo(15));
        }

        [TestCase(-1)]
        [TestCase(40)]
        public void InvalidEdgeID_GetAdjacentBoxesThrowsException(
            int edgeId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => BoardTopology.GetAdjacentBoxIDs(edgeId));
        }
    }
}