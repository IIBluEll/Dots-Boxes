using System;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Shared.Tests
{
    public sealed class BoardDataTests
    {
        [TestCase(0)]
        [TestCase(19)]
        [TestCase(20)]
        [TestCase(39)]
        public void EdgeData_NewEdge_HasNoOwner(int edgeId)
        {
            EdgeData edge = new EdgeData(edgeId);

            Assert.That(edge.EdgeId , Is.EqualTo(edgeId));
            Assert.That(
                edge.OwnerPlayerIndex ,
                Is.EqualTo(PLAYER_INDEX_ENUM.NONE));

            Assert.That(edge.IsConfirmed , Is.False);
        }

        [TestCase(-1)]
        [TestCase(40)]
        public void EdgeData_InvalidID_ThrowsException(int edgeId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new EdgeData(edgeId));
        }

        [Test]
        public void BoxData_FirstBox_HasExpectedEdges()
        {
            BoxData box = new BoxData(0);

            Assert.That(box.BoxId , Is.EqualTo(0));
            Assert.That(box.TopEdgeId , Is.EqualTo(0));
            Assert.That(box.BottomEdgeId , Is.EqualTo(4));
            Assert.That(box.LeftEdgeId , Is.EqualTo(20));
            Assert.That(box.RightEdgeId , Is.EqualTo(21));

            Assert.That(
                box.OwnerPlayerIndex ,
                Is.EqualTo(PLAYER_INDEX_ENUM.NONE));

            Assert.That(box.IsOwned , Is.False);
        }

        [Test]
        public void BoxData_LastBox_HasExpectedEdges()
        {
            BoxData box = new BoxData(15);

            Assert.That(box.TopEdgeId , Is.EqualTo(15));
            Assert.That(box.BottomEdgeId , Is.EqualTo(19));
            Assert.That(box.LeftEdgeId , Is.EqualTo(38));
            Assert.That(box.RightEdgeId , Is.EqualTo(39));
        }

        [TestCase(-1)]
        [TestCase(16)]
        public void BoxData_InvalidID_ThrowsException(int boxId)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BoxData(boxId));
        }
    }
}