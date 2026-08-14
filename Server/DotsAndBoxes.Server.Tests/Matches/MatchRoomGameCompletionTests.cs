using DotsAndBoxes.Server.Matches;
using DotsAndBoxes.Shared;
using NUnit.Framework;

namespace DotsAndBoxes.Server.Tests.Matches
{
    [TestFixture]
    public sealed class MatchRoomGameCompletionTests
    {
        private static readonly DateTimeOffset TURN_DEADLINE_UTC =
            new DateTimeOffset(2030, 1, 1, 0, 0, 20, TimeSpan.Zero);

        [Test]
        public async Task ConfirmEdge_ThroughFullBoard_FinishesMatchAsDraw()
        {
            MatchRoom room = CreateRoom();

            for ( int edgeId = 0; edgeId < BoardTopology.EDGE_COUNT; edgeId++ )
            {
                Guid currentUserId =
                    room.CurrentPlayerIndex == PLAYER_INDEX_ENUM.PLAYER_ONE
                        ? room.PlayerOne.UserId
                        : room.PlayerTwo.UserId;

                ConfirmEdgeRequest request = new ConfirmEdgeRequest
                {
                    MatchId = room.MatchId,
                    EdgeId = edgeId,
                    ExpectedRevision = room.Revision,
                    RequestId = Guid.NewGuid()
                };

                ConfirmEdgeResponse response =
                    await room.ConfirmEdge_async(currentUserId, request);

                Assert.That(
                    response.IsAccepted ,
                    Is.True ,
                    $"Edge {edgeId} 확정에 실패했습니다. Error: {response.Error}");
            }

            MatchSnapshot finalSnapshot =
                await room.CreateSnapshot_async();

            int confirmedEdgeCount = finalSnapshot.EdgeOwners.Count(
                owner => owner != PLAYER_INDEX_ENUM.NONE);

            int ownedBoxCount = finalSnapshot.BoxOwners.Count(
                owner => owner != PLAYER_INDEX_ENUM.NONE);

            ConfirmEdgeRequest afterFinishRequest = new ConfirmEdgeRequest
            {
                MatchId = room.MatchId,
                EdgeId = 0,
                ExpectedRevision = room.Revision,
                RequestId = Guid.NewGuid()
            };

            ConfirmEdgeResponse afterFinishResponse =
                await room.ConfirmEdge_async(
                    room.PlayerOne.UserId,
                    afterFinishRequest);

            Assert.Multiple(() =>
            {
                Assert.That(finalSnapshot.Revision , Is.EqualTo(BoardTopology.EDGE_COUNT));
                Assert.That(confirmedEdgeCount , Is.EqualTo(BoardTopology.EDGE_COUNT));
                Assert.That(ownedBoxCount , Is.EqualTo(BoardTopology.BOX_COUNT));

                Assert.That(finalSnapshot.PlayerOneScore , Is.EqualTo(8));
                Assert.That(finalSnapshot.PlayerTwoScore , Is.EqualTo(8));
                Assert.That(finalSnapshot.GameResult , Is.EqualTo(GAME_RESULT_ENUM.DRAW));

                Assert.That(
                    finalSnapshot.MatchState ,
                    Is.EqualTo(SERVER_MATCH_STATE_ENUM.FINISHING));

                Assert.That(
                    finalSnapshot.FinishReason ,
                    Is.EqualTo(MATCH_FINISH_REASON_ENUM.BOARD_COMPLETED));

                Assert.That(finalSnapshot.TurnDeadlineUtc , Is.Null);

                Assert.That(afterFinishResponse.IsAccepted , Is.False);
                Assert.That(
                    afterFinishResponse.Error ,
                    Is.EqualTo(MATCH_COMMAND_ERROR_ENUM.MATCH_NOT_ACTIVE));

                Assert.That(room.Revision , Is.EqualTo(BoardTopology.EDGE_COUNT));
            });
        }

        private static MatchRoom CreateRoom()
        {
            MatchPlayer playerOne =
                new MatchPlayer(Guid.NewGuid(), "connection-one");

            MatchPlayer playerTwo =
                new MatchPlayer(Guid.NewGuid(), "connection-two");

            return new MatchRoom(
                Guid.NewGuid() ,
                playerOne ,
                playerTwo ,
                PLAYER_INDEX_ENUM.PLAYER_ONE ,
                TURN_DEADLINE_UTC);
        }
    }
}