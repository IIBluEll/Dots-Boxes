using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomConfirmEdgeTests
    {
        [Test]
        public async Task ConfirmEdge_WithValidRequest_ConfirmsEdgeAndIncreasesRevision()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                0,
                0,
                Guid.NewGuid());

            ConfirmEdgeResponse response =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.True);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NONE));
                Assert.That(response.RequestId , Is.EqualTo(request.RequestId));
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(response.Snapshot.Revision , Is.EqualTo(1));
                Assert.That(response.Snapshot.EdgeOwners[ 0 ] , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_ONE));
                Assert.That(response.Snapshot.CurrentPlayerIndex , Is.EqualTo(PLAYER_INDEX_ENUM.PLAYER_TWO));
                Assert.That(room.Revision , Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithDifferentMatchId_ReturnsMatchNotFound()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                Guid.NewGuid(),
                0,
                0,
                Guid.NewGuid());

            ConfirmEdgeResponse response =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_FOUND));
                Assert.That(response.Snapshot , Is.Null);
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithUnknownUser_ReturnsNotMatchPlayer()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                0,
                0,
                Guid.NewGuid());

            ConfirmEdgeResponse response = await room.ConfirmEdge_async(Guid.NewGuid(), request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NOT_A_MATCH_PLAYER));
                Assert.That(response.Snapshot , Is.Null);
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithEmptyRequestId_ReturnsInvalidRequest()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                0,
                0,
                Guid.Empty);

            ConfirmEdgeResponse response =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.INVALID_REQUEST));
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithWrongRevision_ReturnsLatestSnapshot()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                0,
                10,
                Guid.NewGuid());

            ConfirmEdgeResponse response =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.REVISION_MISMATCH));
                Assert.That(response.ShouldRequestSync , Is.True);
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(response.Snapshot.Revision , Is.EqualTo(0));
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ConfirmEdge_WhenNotPlayerTurn_ReturnsNotYourTurn()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                0,
                0,
                Guid.NewGuid());

            ConfirmEdgeResponse response = await room.ConfirmEdge_async(room.PlayerTwo.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.NOT_YOUR_TURN));
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        [Test]
        public async Task ConfirmEdge_WithInvalidEdge_ReturnsInvalidEdge()
        {
            MatchRoom room = CreateRoom();

            ConfirmEdgeRequest request = CreateRequest(
                room.MatchId,
                BoardTopology.EDGE_COUNT,
                0,
                Guid.NewGuid());

            ConfirmEdgeResponse response =
                await room.ConfirmEdge_async(room.PlayerOne.UserId, request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsAccepted , Is.False);
                Assert.That(response.Error , Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.INVALID_EDGE));
                Assert.That(response.Snapshot , Is.Not.Null);
                Assert.That(room.Revision , Is.EqualTo(0));
            });
        }

        private static MatchRoom CreateRoom()
        {
            MatchPlayer playerOne = new MatchPlayer(Guid.NewGuid());
            MatchPlayer playerTwo = new MatchPlayer(Guid.NewGuid());

            return new MatchRoom(
                Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                PLAYER_INDEX_ENUM.PLAYER_ONE);
        }

        private static ConfirmEdgeRequest CreateRequest(
            Guid matchId ,
            int edgeId ,
            long expectedRevision ,
            Guid requestId)
        {
            return new ConfirmEdgeRequest
            {
                MatchId = matchId ,
                EdgeId = edgeId ,
                ExpectedRevision = expectedRevision ,
                RequestId = requestId
            };
        }
    }
}
